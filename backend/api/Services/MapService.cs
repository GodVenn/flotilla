﻿using System.Drawing;
using System.Globalization;
using Api.Database.Models;
using Api.Options;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Api.Services
{
    public interface IMapService
    {
        public abstract Task<Image> FetchMapImage(Mission mission);
        public abstract Task AssignMapToMission(Mission mission);
    }

    public class MapService : IMapService
    {
        private readonly ILogger<MapService> _logger;
        private readonly IOptions<AzureAdOptions> _azureOptions;
        private readonly IOptions<MapBlobOptions> _blobOptions;

        public MapService(
            ILogger<MapService> logger,
            IOptions<AzureAdOptions> azureOptions,
            IOptions<MapBlobOptions> blobOptions
        )
        {
            _logger = logger;
            _azureOptions = azureOptions;
            _blobOptions = blobOptions;
        }

        public async Task<Image> FetchMapImage(Mission mission)
        {
            return await DownloadMapImageFromBlobStorage(mission);
        }

        public async Task AssignMapToMission(Mission mission)
        {
            string mostSuitableMap;
            var boundaries = new Dictionary<string, Boundary>();
            var imageSizes = new Dictionary<string, int[]>();
            var blobContainerClient = GetBlobContainerClient(
                mission.AssetCode.ToLower(CultureInfo.CurrentCulture)
            );
            try
            {
                var resultSegment = blobContainerClient
                    .GetBlobsAsync(BlobTraits.Metadata)
                    .AsPages();

                await foreach (var blobPage in resultSegment)
                {
                    foreach (var blobItem in blobPage.Values)
                    {
                        try
                        {
                            boundaries.Add(blobItem.Name, ExtractMapMetadata(blobItem));
                            imageSizes.Add(blobItem.Name, ExtractImageSize(blobItem));
                        }
                        catch (FormatException)
                        {
                            continue;
                        }
                    }
                }
            }
            catch (RequestFailedException e)
            {
                _logger.LogWarning(
                    "Unable to find any map files for asset code {AssetCode}: {error message}",
                    mission.AssetCode,
                    e.Message
                );
                return;
            }
            try
            {
                mostSuitableMap = FindMostSuitableMap(boundaries, mission.Tasks);
            }
            catch (ArgumentOutOfRangeException)
            {
                _logger.LogWarning("Unable to find a map for mission '{missionId}'", mission.Id);
                return;
            }
            var map = new MissionMap
            {
                MapName = mostSuitableMap,
                Boundary = boundaries[mostSuitableMap],
                TransformationMatrices = new TransformationMatrices(
                    boundaries[mostSuitableMap].AsMatrix()[0],
                    boundaries[mostSuitableMap].AsMatrix()[1],
                    imageSizes[mostSuitableMap][0],
                    imageSizes[mostSuitableMap][1]
                )
            };
            mission.Map = map;
        }

        private BlobContainerClient GetBlobContainerClient(string asset)
        {
            var serviceClient = new BlobServiceClient(
                new Uri($"https://{_blobOptions.Value.StorageAccount}.blob.core.windows.net"),
                new ClientSecretCredential(
                    _azureOptions.Value.TenantId,
                    _azureOptions.Value.ClientId,
                    _azureOptions.Value.ClientSecret
                )
            );
            var containerClient = serviceClient.GetBlobContainerClient(asset);
            return containerClient;
        }

        private async Task<Image> DownloadMapImageFromBlobStorage(Mission currentMission)
        {
            var blobContainerClient = GetBlobContainerClient(
                currentMission.AssetCode.ToLower(CultureInfo.CurrentCulture)
            );
            var blobClient = blobContainerClient.GetBlobClient(currentMission.Map.MapName);

            await using var stream = await blobClient.OpenReadAsync();
            return Image.FromStream(stream);
        }

        private Boundary ExtractMapMetadata(BlobItem map)
        {
            try
            {
                double lowerLeftX =
                    double.Parse(map.Metadata["lowerLeftX"], CultureInfo.CurrentCulture) / 1000;
                double lowerLeftY =
                    double.Parse(map.Metadata["lowerLeftY"], CultureInfo.CurrentCulture) / 1000;
                double upperRightX =
                    double.Parse(map.Metadata["upperRightX"], CultureInfo.CurrentCulture) / 1000;
                double upperRightY =
                    double.Parse(map.Metadata["upperRightY"], CultureInfo.CurrentCulture) / 1000;
                return new Boundary(lowerLeftX, lowerLeftY, upperRightX, upperRightY);
            }
            catch (FormatException e)
            {
                _logger.LogWarning(
                    "Unable to extract metadata from map {map.Name}: {e.Message}",
                    map.Name,
                    e.Message
                );
                throw e;
            }
        }

        private int[] ExtractImageSize(BlobItem map)
        {
            try
            {
                int x = int.Parse(map.Metadata["imageWidth"], CultureInfo.CurrentCulture);
                int y = int.Parse(map.Metadata["imageHeight"], CultureInfo.CurrentCulture);
                return new int[] { x, y };
            }
            catch (FormatException e)
            {
                _logger.LogWarning(
                    "Unable to extract image size from map {map.Name}: {e.Message}",
                    map.Name,
                    e.Message
                );
                throw e;
            }
        }

        private static string FindMostSuitableMap(
            Dictionary<string, Boundary> boundaries,
            IList<PlannedTask> tasks
        )
        {
            string mostSuitableMap = "";
            foreach (var boundary in boundaries)
            {
                if (!string.IsNullOrEmpty(mostSuitableMap))
                {
                    string referenceMap = mostSuitableMap;
                    //If the current map is lower resolution than the best map, it's not worth checking.
                    if (
                        !CheckMapIsHigherResolution(
                            boundary.Value.AsMatrix(),
                            boundaries[referenceMap].AsMatrix()
                        )
                    )
                    {
                        continue;
                    }
                }
                if (CheckTagsInBoundary(boundary.Value.AsMatrix(), tasks))
                {
                    mostSuitableMap = boundary.Key;
                }
            }
            if (string.IsNullOrEmpty(mostSuitableMap))
            {
                throw new ArgumentOutOfRangeException(nameof(tasks));
            }
            return mostSuitableMap;
        }

        private static bool CheckTagsInBoundary(List<double[]> boundary, IList<PlannedTask> tasks)
        {
            foreach (var task in tasks)
            {
                if (task.TagPosition.X < boundary[0][0] | task.TagPosition.X > boundary[1][0])
                {
                    return false;
                }
                if (task.TagPosition.Y < boundary[0][1] | task.TagPosition.Y > boundary[1][1])
                {
                    return false;
                }
            }
            return true;
        }

        private static bool CheckMapIsHigherResolution(
            List<double[]> checkMap,
            List<double[]> referenceMap
        )
        {
            if (checkMap[0][0] < referenceMap[0][0] | checkMap[0][1] < referenceMap[0][1])
            {
                return false;
            }
            if (checkMap[1][0] > referenceMap[1][0] | checkMap[1][1] > referenceMap[1][1])
            {
                return false;
            }
            return true;
        }
    }
}
