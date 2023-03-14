using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.Database.Models;
using Api.Services;

namespace Api.Test.Mocks
{
    public class MockMapService : IMapService
    {
        public async Task AssignMapToMission(Mission mission)
        {
            await Task.Run(() => Thread.Sleep(1));
        }

        public async Task<Image> FetchMapImage(Mission mission)
        {
            await Task.Run(() => Thread.Sleep(1));
            string filePath = Directory.GetCurrentDirectory() + "Images/MockMapImage.png";
            return Image.FromFile(filePath);
        }
    }
}
