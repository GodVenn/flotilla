﻿namespace Api.Controllers.Models
{
    public struct ScheduledMissionQuery
    {
        public string RobotId { get; set; }
        public int EchoMissionId { get; set; }
        public DateTimeOffset DesiredStartTime { get; set; }
        public string AssetCode { get; set; }
    }
}
