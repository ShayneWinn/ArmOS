using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game.AI;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Scripting;
using VRage.Scripting.MemorySafeTypes;
using VRageMath;

namespace IngameScript
{

    /// <summary>
    /// The ArmModule extension module.
    /// </summary>
    
    public class ArmModule: BaseExtensionModule
    {

        //  //==========\\
        //  ||  MOTHER  ||
        //  \\==========//

        /// <summary>
        /// Constructor. In most cases, you should use the boot method to set up this module.
        /// </summary>
        /// <param name="mother"></param>
        public ArmModule(Mother mother) : base(mother) {
            
        }

        /// <summary>
        /// Boot the module, register commands, subscribe to events,
        /// and reference other modules registered with Mother.
        /// Find all motors with the tag "arm/motor" and initialize them.
        /// </summary>
        public override void Boot()
        {
            Bus = Mother.GetModule<CommandBus>();
            Catalogue = Mother.GetModule<BlockCatalogue>();

            MotorNames = new List<string>();
            Motors = new Dictionary<string, IMyMotorStator>();

            Catalogue.GetBlocksByName<IMyMotorStator>("#arm/motor");
            foreach (var motor in Catalogue.GetBlocksByName<IMyMotorStator>("#arm/motor"))
            {
                Mother.Print("$Found motor: " + motor.CustomName);
                MotorNames.Add(motor.CustomName);
                Motors[motor.CustomName] = motor;
            }
            MotorNames.Sort((a, b) => {
                var aid = this.Catalogue.GetBlockConfiguration(this.Motors[a]).Get("general", "arm/id").ToInt32(-1);
                var bid = this.Catalogue.GetBlockConfiguration(this.Motors[b]).Get("general", "arm/id").ToInt32(-1);
                if (aid == -1)
                    Mother.Print($"Error: motor '{a}' is missing configuration 'arm/id'");
                if (bid == -1)
                    Mother.Print($"Error: motor '{b}' is missing configuration 'arm/id'");
                return aid.CompareTo(bid);
            });


            //MOTHER... HEAR ME!!!
            RegisterCommand(new HomeArmCommand(this));
            RegisterCommand(new RotorRotateCommand(this));
            RegisterCommand(new MoveArmCommand(this));
            RegisterCommand(new MoveAllCommand(this));
        }

        /// <summary>
        /// Run the module. This method is called each program cycle. You should limit 
        /// use of this method to essential tasks that need to run frequently.
        /// </summary>
        public override void Run()
        {
            //
        }


        //  //==========\\
        //  ||  Public  ||
        //  \\==========//

        public IMyMotorStator GetMotorByName(string name)
        {
            if (Motors.ContainsKey(name))
                return Motors[name];
            else
                return null;
        }


        //  //=========\\
        //  ||  Logic  ||
        //  \\=========//

        private List<string> MotorNames;
        private Dictionary<string, IMyMotorStator> Motors;

        CommandBus Bus;
        BlockCatalogue Catalogue;

        private double[] InverseKinematics(double x, double y)
        {
            double armlength = 3f;
            double baselength = Math.Sqrt(x * x + y * y);
            double baseAngle = Math.Atan2(y, x) - Math.PI/2; // compensate for rotor orientation
            double shoulderAngle = Math.Asin( (baselength-1)/2 / armlength );
            double elbowangle = (Math.PI/2) - shoulderAngle;

            baseAngle = MathHelper.ToDegrees(baseAngle);
            shoulderAngle = MathHelper.ToDegrees(shoulderAngle);
            elbowangle = MathHelper.ToDegrees(elbowangle);

            return new double[] {
                baseAngle,
                shoulderAngle,
                elbowangle,
                elbowangle,
                shoulderAngle,
                baseAngle
            };
        }

        private bool IsMotorAtTarget(IMyMotorStator motor, double angle)
        {
            var actual = MathHelper.ToDegrees(motor.Angle);
            double delta = mod(angle - actual + 180, 360) - 180;
            return Math.Abs(delta) <= 1;
        }

        private double[] targetAngles;
        public string MoveTo(double x, double y, double seconds)
        {
            targetAngles = InverseKinematics(x, y);
            Mother.Print($"Moving {Motors.Count} to {targetAngles.Length} angles");
            for (int i = 0; i < targetAngles.Length; i++)
            {
                var motor = Motors[MotorNames[i]];
                var targetAngle = targetAngles[i];
                StartMotor(motor, targetAngle, seconds);
                Mother.GetModule<ActivityMonitor>().RegisterBlock(
                    motor,
                    block => IsMotorAtTarget(block as IMyMotorStator, targetAngle),
                    block => StopMotor(block as IMyMotorStator)
                );
            }
            
            return $"Moving ({x}, {y}) over {seconds}s";
        }

        public string MoveTo(PathPoint target)
        {
            return MoveTo(target.x, target.y, target.t);
        }

        private void StartTo(PathPoint point)
        {
            targetAngles = InverseKinematics(point.x, point.y);
            for (int i = 0; i < MotorNames.Count; i++)
            {
                var motor = Motors[MotorNames[i]];
                StartMotor(motor, targetAngles[i], point.t);
            }
        }

        private double mod (double a, double n) { return (a % n + n) % n; }

        public void StartMotor(IMyMotorStator motor, double angle, double seconds)
        {
            var actual = MathHelper.ToDegrees(motor.Angle);
            double delta = mod(angle - actual + 180, 360) - 180;
            var degps = delta / seconds;
            var rpm = MathHelper.Clamp(degps/6, -10, 10);
            motor.TargetVelocityRPM = (float)rpm;
            motor.Enabled = true;
            
            Mother.Print($"{motor.CustomName} => {angle:F2} @ {rpm:F2}");
        }
        private void StopMotor(IMyMotorStator motor)
        {
            motor.TargetVelocityRad = 0;
            //motor.Enabled = false;
            //Mother.GetModule<ActivityMonitor>().UnregisterBlock(motor);
        }

        /// <summary>
        /// Home the arm to its default position.
        /// </summary>
        public void HomeArm()
        {
            MoveTo(0, 1, 5);
        }

        

        private bool isAtTarget(IMyMotorStator motor, double targetAngle)
        {
            return Math.Abs(motor.Angle - targetAngle) <= 1;
        }


        private List<PathPoint> positionQueue = new List<PathPoint>();
        public string MoveAll()
        {
            //positionQueue.Add(new PathPoint(0, 1, 1));
            //positionQueue.Add(new PathPoint(0, 2, 1));
            //positionQueue.Add(new PathPoint(0, 3, 1));
            //positionQueue.Add(new PathPoint(0, 4, 1));
            //positionQueue.Add(new PathPoint(2, 4, 1.8));
            //positionQueue.Add(new PathPoint(2, 2, 1.8));
            //positionQueue.Add(new PathPoint(2, 0, 1.8));
            //positionQueue.Add(new PathPoint(2, -2, 1.8));
            //positionQueue.Add(new PathPoint(2, -4, 1.8));
            //positionQueue.Add(new PathPoint(2, 0, 4));
            //positionQueue.Add(new PathPoint(0, 4, 4));
            //positionQueue.Add(new PathPoint(0, 1, 6));

            positionQueue.Add(new PathPoint(0, 1, 5));
            positionQueue.Add(new PathPoint(0, 3, 2));
            positionQueue.Add(new PathPoint(3, 3, 2));
            positionQueue.Add(new PathPoint(3, -3, 2));
            positionQueue.Add(new PathPoint(3, 0, 2, 2));
            positionQueue.Add(new PathPoint(3, 3, 2, 1));
            positionQueue.Add(new PathPoint(0, 3, 2, 1));
            positionQueue.Add(new PathPoint(0, 1, 5));


            List<PathPoint> processedPoints;
            PathPlotter plotter = new PathPlotter(positionQueue);
            Mother.Print($"Processing {positionQueue.Count} points");
            processedPoints = plotter.Plot(positionQueue, .1);
            positionQueue = processedPoints;
            MoveArmPop();
            return $"Moving {positionQueue.Count} points";
        }
        private void MoveArmPop()
        {
            if (positionQueue.Count == 0)
                return;
            PathPoint target = positionQueue[0];
            if (positionQueue.Count == 1)
            {
                MoveTo(target);
            } else
            {
                StartTo(target);
                Mother.Wait(() => MoveArmPop(), target.t);
            }
            positionQueue.RemoveAt(0);
        }

        /// <summary>
        /// Handle events to which this module is subscribed.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="eventData"></param>
        public override void HandleEvent(IEvent e, object eventData)
        {
            //
        }
    }
}
