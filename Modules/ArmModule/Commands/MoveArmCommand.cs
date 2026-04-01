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
    /// The MoveArmCommand command.
    /// </summary>
    public class MoveArmCommand : BaseModuleCommand
    {
        /// <summary>
        /// The ArmModule extension module.
        /// </summary>
        readonly ArmModule Module;

        /// <summary>
        /// The name of the command.
        /// </summary>
        public override string Name => "arm/move";

        /// <summary>
        /// Constructor. We instantiate the command with a reference to the module 
        /// it belongs to so that it may access logic within the module.
        /// </summary>
        /// <param name="module"></param>
        public MoveArmCommand(ArmModule module)
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
            if (command.Arguments.Count == 2)
            {
                command.Arguments.Add("1");
            }
            if (command.Arguments.Count != 3)
                return $"Usage: arm/move [x] [y] (seconds)";
            
            double x, y, seconds;
            if (!double.TryParse(command.Arguments[0], out x))
                return $"Error: could not parse x '{command.Arguments[0]}'";
            if (!double.TryParse(command.Arguments[1], out y))
                return $"Error: could not parse y '{command.Arguments[1]}'";
            if (!double.TryParse(command.Arguments[2], out seconds))
                return $"Error: could not parse seconds '{command.Arguments[2]}'";

            return Module.StartTo(new PathPoint(x, y, seconds));
        }
    }
}
