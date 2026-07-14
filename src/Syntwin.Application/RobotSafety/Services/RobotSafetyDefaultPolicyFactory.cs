using Syntwin.Application.RobotSafety.Interfaces;
using Syntwin.Application.RobotSafety.Policies;

namespace Syntwin.Application.RobotSafety.Services;

public sealed class RobotSafetyDefaultPolicyFactory : IRobotSafetyDefaultPolicyFactory
{
    public RobotSafetyPolicyDefinition CreateDefaultPolicy(string? robotModel)
    {
        return new RobotSafetyPolicyDefinition
        {
            Name = "Fairino FR5 default safety policy",
            RobotModel = string.IsNullOrWhiteSpace(robotModel)
                ? "Fairino FR5"
                : robotModel.Trim(),

            JointLimits =
            [
                new RobotJointLimit { Joint = 1, MinDeg = -175, MaxDeg = 175 },
                new RobotJointLimit { Joint = 2, MinDeg = -265, MaxDeg = 85 },
                new RobotJointLimit { Joint = 3, MinDeg = -160, MaxDeg = 160 },
                new RobotJointLimit { Joint = 4, MinDeg = -265, MaxDeg = 265 },
                new RobotJointLimit { Joint = 5, MinDeg = -175, MaxDeg = 175 },
                new RobotJointLimit { Joint = 6, MinDeg = -175, MaxDeg = 175 }
            ],

            TcpWorkspace = new RobotTcpWorkspaceLimit
            {
                MinX = -900,
                MaxX = 900,
                MinY = -900,
                MaxY = 900,
                MinZ = 0,
                MaxZ = 1200,
                MinRotationDeg = -360,
                MaxRotationDeg = 360
            },

            MinSpeedPercent = 1,
            MaxSpeedPercent = 100,
            MinAccelerationPercent = 1,
            MaxAccelerationPercent = 100,
            MaxJointDeltaDegPerStep = 120,
            MaxFirstStepJointDeltaDeg = 120
        };
    }
}