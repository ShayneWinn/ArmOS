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
    /// The ActiveMotorModule extension module.
    /// </summary>
    public class ActiveMotorModule: BaseExtensionModule
    {
        /// <summary>
        /// Constructor. In most cases, you should use the boot method to set up this module.
        /// </summary>
        /// <param name="mother"></param>
        public ActiveMotorModule(Mother mother) : base(mother) {
            //
        }

        /// <summary>
        /// Boot the module. This is where you should register commands, subscribe 
        /// to events, and reference other modules registered with Mother.
        /// </summary>
        public override void Boot()
        {
            //
            RegisterCommand(new AddMotorsCommand(this));
            RegisterCommand(new ActivateMotorsCommand(this));
            RegisterCommand(new DeactivateMotorsCommand(this));
            RegisterCommand(new ListCommand(this));
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
