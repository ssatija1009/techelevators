using static cgmtwiningestfunction.Models.enums;

namespace cgmtwiningestfunction.DataObjects
{
    public class SurveyInfo
    {
        public VisionStatusEnum VisionStatus { get; set; }

        public FatigueStatusEnum FatigueStatus { get; set; }

        public int SleepHours { get; set; }

        public WaterConsumptionStatusEnum WaterConsumptionStatus { get; set; }

        public UrinationStatusEnum UrinationStatus { get; set; }

        public CutsHealingStatusEnum CutsHealingStatus { get; set; }

        public TinglingSensationStatusEnum TinglingSensationStatus { get; set; }

        public WeightStatusEnum WeightStatus { get; set; }
    }
}

