using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        IMyMotorStator baseRotor;
        IMyMotorStator shoulderHinge;
        IMyMotorStator elbowHingea;
        IMyMotorStator elbowHingeb;
        IMyMotorStator wristHinge;
        IMyMotorStator wristRotor;

        CommandBus bus;

        /// <summary>
        /// Constructor. In most cases, you should use the boot method to set up this module.
        /// </summary>
        /// <param name="mother"></param>
        public ArmModule(Mother mother) : base(mother) {
            
        }

        /// <summary>
        /// Boot the module. This is where you should register commands, subscribe 
        /// to events, and reference other modules registered with Mother.
        /// </summary>
        public override void Boot()
        {
            RegisterCommand(new HomeArmCommand(this));

            bus = Mother.GetModule<CommandBus>();
            

            BlockCatalogue catalogue = Mother.GetModule<BlockCatalogue>();
            Mother.Print($"{catalogue.GetBlocks<IMyTerminalBlock>().Count} blocks found in catalogue.");
            this.baseRotor = catalogue.GetBlocksByName<IMyMotorStator>("Rotor.Base").FirstOrDefault();
            if (baseRotor == null)
                throw new Exception("Base rotor not found! Make sure to name a block 'Rotor.Base' in order for the ArmModule to work.");
            this.shoulderHinge = catalogue.GetBlocksByName<IMyMotorStator>("Hinge.Shoulder").FirstOrDefault();
            this.elbowHingea = catalogue.GetBlocksByName<IMyMotorStator>("Hinge.Elbow.A").FirstOrDefault();
            this.elbowHingeb = catalogue.GetBlocksByName<IMyMotorStator>("Hinge.Elbow.B").FirstOrDefault();
            this.wristHinge = catalogue.GetBlocksByName<IMyMotorStator>("Hinge.Wrist").FirstOrDefault();
            this.wristRotor = catalogue.GetBlocksByName<IMyMotorStator>("Rotor.Wrist").FirstOrDefault();
        }

        private bool MoveTo(double x, double y, double seconds)
        {
            Mother.Print($"Moving arm to ({x}, {y}) over {seconds} seconds...");
            double armlength = 3f;
            double baselength = Math.Sqrt(x * x + y * y);
            double baseAngle = Math.Atan2(y, x);
            double shoulderAngle = Math.Acos( (baselength-1)/2 / armlength );
            double elbowangle = (Math.PI/2) - shoulderAngle;

            StartMotor(baseRotor, baseAngle, seconds);
            //StartMotor(shoulderHinge, shoulderAngle, seconds);
            //StartMotor(elbowHingea, elbowangle, seconds);
            //StartMotor(elbowHingeb, elbowangle, seconds);
            //StartMotor(wristHinge, 0, seconds);
            //StartMotor(wristRotor, 0, seconds);

            return true;
        }

        private bool isAtTarget(IMyMotorStator motor, double targetAngle)
        {
            return Math.Abs(motor.Angle - targetAngle) < 2;
        }

        private void StartMotor(IMyMotorStator motor, double angle, double seconds=1)
        {
            foreach (var em in Mother.CoreModules.ToTrackedImmutableArray())
            {
                Mother.Print($"Module: {em.Value.GetType().Name}");
            }
            Mother.Print($"Starting motor {motor.CustomName} to move to {angle} radians over {seconds} seconds...");
            double rpm = MathHelper.ToDegrees(angle - motor.Angle) / seconds * 60;
            bus.RunTerminalCommand("rotor/rotate Rotor.Base 90");
        }

        private void StopMotor(IMyMotorStator motor)
        {
            motor.TargetVelocityRad = 0;
            motor.Enabled = false;
        }

        /// <summary>
        /// Home the arm to its default position.
        /// </summary>
        public void HomeArm()
        {
            MoveTo(0, 1, 5);
        }

        /// <summary>
        /// Run the module. This method is called each program cycle. You should limit 
        /// use of this method to essential tasks that need to run frequently.
        /// </summary>
        public override void Run()
        {
            //
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
