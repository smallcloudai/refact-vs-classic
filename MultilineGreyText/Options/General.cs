﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using OutlineRegionTest.Properties;

namespace RefactAI{

    //This uses visual studio toolkit to create an options page
    //The GeneralOptions xaml file controls the presentation of the page
    internal partial class OptionsProvider {
        // Register the options with these attributes in your package class:
        // [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "My options", "General", 0, 0, true)]
        // [ProvideProfile(typeof(OptionsProvider.GeneralOptions), "My options", "General", 0, 0, true)]
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General> { }
    }

    public class General : BaseOptionModel<General>{

        //Each of these variables corresponds to a setting in the Options dialog.
        [Category("Refact Assistant")]
        [DisplayName("Address URL")]
        [Description("This is a string.")]
        [DefaultValue("")]
        public string AddressURL { get; set; } = "";

        [Category("Refact Assistant")]
        [DisplayName("API Key")]
        [Description("This is a string.")]
        [DefaultValue("")]
        public string APIKey { get; set; } = "";

        [Category("Refact Assistant")]
        [DisplayName("Code Completion Model")]
        [Description("This is a string.")]
        [DefaultValue("")]
        public string CodeCompletionModel{ get; set; } = "";

        [Category("Refact Assistant")]
        [DisplayName("Code Completion Model Other")]
        [Description("This is a string.")]
        [DefaultValue("")]
        public string CodeCompletionModelOther{ get; set; } = "";

        [Category("Refact Assistant")]
        [DisplayName("Code Completion Scratchpad")]
        [Description("This is a string.")]
        [DefaultValue("")]
        public string CodeCompletionScratchpad { get; set; } = "";

        [Category("Refact Assistant")]
        [DisplayName("Pause Completion")]
        [Description("An informative description.")]
        [DefaultValue(false)]
        public bool PauseCompletion{ get; set; } = false;

        [Category("Refact Assistant")]
        [DisplayName("Telemetry Code Snippets")]
        [Description("An informative description.")]
        [DefaultValue(false)]
        public bool TelemetryCodeSnippets{ get; set; } = false;

        [Category("Refact Assistant")]
        [DisplayName("Insecure SSL")]
        [Description("An informative description.")]
        [DefaultValue(false)]
        public bool InsecureSSL { get; set; } = false;

        // Event handler to be invoked when settings are saved
        private void OnSettingsSaved(General options)
        {
            OnSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        // Event to notify when settings are changed
        public static event EventHandler OnSettingsChanged;
        public General() : base(){
            Saved += OnSettingsSaved;
        }
    }
}