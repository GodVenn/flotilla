using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api.Controllers.Models;
using Api.Services.Models;
using Microsoft.EntityFrameworkCore;

#nullable disable

namespace Api.Database.Models
{
    [Owned]
    public class MissionTask
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }

        [MaxLength(200)]
        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
        public string IsarTaskId { get; private set; } = Guid.NewGuid().ToString();

        [Required]
        public int TaskOrder { get; set; }

        [MaxLength(200)]
        public string TagId { get; set; }

        [MaxLength(200)]
        public Uri EchoTagLink { get; set; }

        [Required]
        public Position InspectionTarget { get; set; }

        [Required]
        public Pose RobotPose { get; set; }

        public int EchoPoseId { get; set; }

        private TaskStatus _taskStatus;

        [Required]
        public TaskStatus TaskStatus
        {
            get { return _taskStatus; }
            set
            {
                _taskStatus = value;
                if (IsCompleted && EndTime is null)
                    EndTime = DateTimeOffset.UtcNow;

                if (_taskStatus is TaskStatus.InProgress && StartTime is null)
                    StartTime = DateTimeOffset.UtcNow;
            }
        }

        public bool IsCompleted =>
            _taskStatus
                is TaskStatus.Cancelled
                    or TaskStatus.Successful
                    or TaskStatus.Failed
                    or TaskStatus.PartiallySuccessful;

        public DateTimeOffset? StartTime { get; private set; }

        public DateTimeOffset? EndTime { get; private set; }

        public IList<Inspection> Inspections { get; set; }

        public MissionTask() { }

        public MissionTask(EchoTag echoTag, Position tagPosition)
        {
            Inspections = echoTag.Inspections
                .Select(inspection => new Inspection(inspection))
                .ToList();
            EchoTagLink = echoTag.URL;
            TagId = echoTag.TagId;
            InspectionTarget = tagPosition;
            RobotPose = echoTag.Pose;
            EchoPoseId = echoTag.PoseId;
            TaskOrder = echoTag.PlanOrder;
        }

        public void UpdateWithIsarInfo(IsarTask isarTask)
        {
            UpdateTaskStatus(isarTask.TaskStatus);
            foreach (var inspection in Inspections)
            {
                var correspondingStep = isarTask.Steps.Single(
                    step =>
                        step.IsarStepId.Equals(
                            inspection.IsarStepId.ToString(),
                            StringComparison.Ordinal
                        )
                );
                inspection.UpdateWithIsarInfo(correspondingStep);
            }
        }

        public void UpdateTaskStatus(IsarTaskStatus isarStatus)
        {
            TaskStatus = isarStatus switch
            {
                IsarTaskStatus.NotStarted => TaskStatus.NotStarted,
                IsarTaskStatus.InProgress => TaskStatus.InProgress,
                IsarTaskStatus.Successful => TaskStatus.Successful,
                IsarTaskStatus.PartiallySuccessful => TaskStatus.PartiallySuccessful,
                IsarTaskStatus.Cancelled => TaskStatus.Cancelled,
                IsarTaskStatus.Paused => TaskStatus.Paused,
                IsarTaskStatus.Failed => TaskStatus.Failed,
                _ => throw new ArgumentException($"ISAR Task status '{isarStatus}' not supported")
            };
        }

#nullable enable
        public Inspection? GetInspectionByIsarStepId(string isarStepId)
        {
            return Inspections.FirstOrDefault(
                inspection => inspection.IsarStepId.Equals(isarStepId, StringComparison.Ordinal)
            );
        }

#nullable disable
    }

    public enum TaskStatus
    {
        Successful,
        PartiallySuccessful,
        NotStarted,
        InProgress,
        Failed,
        Cancelled,
        Paused,
    }
}
