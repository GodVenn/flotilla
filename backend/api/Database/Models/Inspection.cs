using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api.Controllers.Models;
using Api.Services.Models;
using Microsoft.EntityFrameworkCore;

#nullable disable
namespace Api.Database.Models
{
    [Owned]
    public class Inspection
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }

        [MaxLength(200)]
        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
        public string IsarStepId { get; private set; } = Guid.NewGuid().ToString();

        private InspectionStatus _inspectionStatus;

        [Required]
        public InspectionStatus InspectionStatus
        {
            get { return _inspectionStatus; }
            set
            {
                _inspectionStatus = value;
                if (IsCompleted && EndTime is null)
                    EndTime = DateTimeOffset.UtcNow;

                if (_inspectionStatus is InspectionStatus.InProgress && StartTime is null)
                    StartTime = DateTimeOffset.UtcNow;
            }
        }

        public bool IsCompleted =>
            _inspectionStatus
                is InspectionStatus.Cancelled
                    or InspectionStatus.Successful
                    or InspectionStatus.Failed;

        [Required]
        public InspectionType InspectionType { get; set; }

        public float? VideoDuration { get; set; }

        [MaxLength(250)]
        public string AnalysisTypes { get; set; }

        [MaxLength(250)]
        public string InspectionUrl { get; set; }

        public DateTimeOffset? StartTime { get; private set; }

        public DateTimeOffset? EndTime { get; private set; }

        public Inspection()
        {
            InspectionType = InspectionType.Image;
        }

        public Inspection(EchoInspection echoInspection)
        {
            InspectionType = echoInspection.InspectionType;
            VideoDuration = echoInspection.TimeInSeconds;
        }

        public void UpdateWithIsarInfo(IsarStep isarStep)
        {
            UpdateInspectionStatus(isarStep.StepStatus);
            InspectionType = isarStep.StepType switch
            {
                IsarStepType.RecordAudio => InspectionType.Audio,
                IsarStepType.TakeImage => InspectionType.Image,
                IsarStepType.TakeThermalImage => InspectionType.ThermalImage,
                IsarStepType.TakeVideo => InspectionType.Video,
                IsarStepType.TakeThermalVideo => InspectionType.ThermalVideo,
                _
                  => throw new ArgumentException(
                      $"ISAR step type '{isarStep.StepType}' not supported for inspections"
                  )
            };
        }

        public void UpdateInspectionStatus(IsarStepStatus isarStatus)
        {
            InspectionStatus = isarStatus switch
            {
                IsarStepStatus.NotStarted => InspectionStatus.NotStarted,
                IsarStepStatus.InProgress => InspectionStatus.InProgress,
                IsarStepStatus.Successful => InspectionStatus.Successful,
                IsarStepStatus.Cancelled => InspectionStatus.Cancelled,
                IsarStepStatus.Failed => InspectionStatus.Failed,
                _
                  => throw new ArgumentException(
                      $"ISAR step status '{isarStatus}' not supported for inspection status"
                  )
            };
        }
    }

    public enum InspectionStatus
    {
        Successful,
        InProgress,
        NotStarted,
        Failed,
        Cancelled
    }

    public enum InspectionType
    {
        Image,
        ThermalImage,
        Video,
        ThermalVideo,
        Audio
    }
}
