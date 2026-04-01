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
using System.Runtime.Hosting;
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
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// The RotorRotateCommand command.
    /// </summary>
    public class RotorRotateCommand : BaseModuleCommand
    {
        /// <summary>
        /// The ArmModule extension module.
        /// </summary>
        readonly ArmModule Module;

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name => "rotor/rotate";

        /// <summary>
        /// Constructor. We instantiate the command with a reference to the module 
        /// it belongs to so that it may access logic within the module.
        /// </summary>
        /// <param name="module"></param>
        public RotorRotateCommand(ArmModule module)
        {
            Module = module;
        }

        /// <summary>
        /// Execute the command. This method is called when the command is invoked 
        /// from the programmable block terminal, or via a trigger like a button 
        /// or timer block. It should return a string that will be displayed
        /// in the terminal.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override string Execute(TerminalCommand command)
        {
            // USAGE rotor/rotate [motor name] [angle in degrees] (options)
            //   --speed=[rpm] : rotate at specified speed
            //   --time=[seconds] : rotate to target angle in specified time (overrides speed)

            // pre-parse arguments
            if (command.Arguments.Count < 2 || command.Arguments.Count > 3)
                return $"Usage: rotor/rotate [motor name] [angle in degrees] (--speed=[rpm], --time=[seconds])";
            if (command.Arguments.Count == 2)
                command.Arguments.Add("--speed=1"); // default to 1 rpm if not specified
            
            // try to parse arguments
            string motorName = command.Arguments[0].Trim();
            IMyMotorStator motor = Module.GetMotorByName(motorName);
            if (motor == null)
            {
                return $"Error: no motor with name '{motorName}' found";
            }

            double angle;
            if (!double.TryParse(command.Arguments[1], out angle))
                return $"Error: could not parse angle '{command.Arguments[1]}'";

            
            foreach (var option in command.Options)
            {
                if (option.Key == "speed")
                {
                    double speed;
                    if (!double.TryParse(option.Value, out speed))
                        return $"Error: could not parse speed '{option.Value}'";
                    motor.RotateToAngle(MyRotationDirection.AUTO, (float)angle, (float)speed);
                    return "moving";
                }
                else if (option.Key == "time")
                {
                    double time;
                    if (!double.TryParse(option.Value, out time))
                        return $"Error: could not parse time '{option.Value}'";
                    // calculate required speed to move to target in specified time
                    double currentAngle = motor.Angle;
                    double angleDifference = Math.Abs(MathHelper.ToDegrees(currentAngle) - angle + 180) % 360 - 180; // shortest angle difference   
                    double rotationPart = angleDifference / 360;
                    double speed = rotationPart / (time / 60); // rpm
                    motor.RotateToAngle(MyRotationDirection.AUTO, (float)angle, (float)speed);
                    return "moving";
                }
                else
                {
                    return $"Error: unrecognized option '--{option.Key}'";
                }
            }

            motor.RotateToAngle(MyRotationDirection.AUTO, (float)angle, 1f);
            return "moving";
        }
    }
}
