#region "copyright"

/*
    Copyright © 2016 - 2021 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Microsoft.Win32;
using NINA.Core.Utility;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;

namespace NINA.Plugin.ExoPlanets {

    /// <summary>
    /// This class exports the IPlugin interface and will be used for the general plugin information and options
    /// An instance of this class will be created and set as datacontext on the plugin options tab in N.I.N.A. to be able to configure global plugin settings
    /// The user interface for the settings will be defined in the Options.xaml
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class ExoPlanets : PluginBase, ISettings, INotifyPropertyChanged {
        private CancellationTokenSource executeCTS;

        [ImportingConstructor]
        public ExoPlanets() {
            if (Properties.Settings.Default.UpgradeSettings) {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeSettings = false;
                CoreUtil.SaveSettings(Properties.Settings.Default);
            }

            OpenFileCommand = new GalaSoft.MvvmLight.Command.RelayCommand<bool>((o) => { using (executeCTS = new CancellationTokenSource()) { OpenFile(); } });
        }

        public ICommand OpenFileCommand { get; private set; }

        public int TargetList {
            get => Properties.Settings.Default.TargetList;
            set {
                Properties.Settings.Default.TargetList = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public bool CheckMagnitude {
            get => Properties.Settings.Default.CheckMagnitude;
            set {
                Properties.Settings.Default.CheckMagnitude = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public double MaxMagnitude {
            get => Properties.Settings.Default.MaxMagnitude;
            set {
                Properties.Settings.Default.MaxMagnitude = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public bool WithinTwilight {
            get => Properties.Settings.Default.WithinTwilight;
            set {
                Properties.Settings.Default.WithinTwilight = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public bool WithinNautical {
            get => Properties.Settings.Default.WithinNautical;
            set {
                Properties.Settings.Default.WithinNautical = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public bool PartialTransits {
            get => Properties.Settings.Default.PartialTransits;
            set {
                Properties.Settings.Default.PartialTransits = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public bool AboveHorizon {
            get => Properties.Settings.Default.AboveHorizon;
            set {
                Properties.Settings.Default.AboveHorizon = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public bool WithoutMeridianFlip {
            get => Properties.Settings.Default.WithoutMeridianFlip;
            set {
                Properties.Settings.Default.WithoutMeridianFlip = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public bool RetrieveComparisonStars {
            get => Properties.Settings.Default.RetrieveComparisonStars;
            set {
                Properties.Settings.Default.RetrieveComparisonStars = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public bool RetrieveVariableStars {
            get => Properties.Settings.Default.RetrieveVariableStars;
            set {
                Properties.Settings.Default.RetrieveVariableStars = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public bool SaveStarList {
            get => Properties.Settings.Default.SaveStarList;
            set {
                Properties.Settings.Default.SaveStarList = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string ExposureTimes {
            get => Properties.Settings.Default.ExposureTimes;
            set {
                Properties.Settings.Default.ExposureTimes = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public int VarStarCatalogTypeIndex {
            get => Properties.Settings.Default.VarStarCatalogTypeIndex;
            set {
                Properties.Settings.Default.VarStarCatalogTypeIndex = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string VarStarCatalog {
            get => Properties.Settings.Default.VarStarCatalog;
            set {
                Properties.Settings.Default.VarStarCatalog = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public int VarStarObservationSpan {
            get => Properties.Settings.Default.VarStarObservationSpan;
            set {
                Properties.Settings.Default.VarStarObservationSpan = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public bool UseExposureTimes {
            get => Properties.Settings.Default.UseExposureTimes;
            set {
                Properties.Settings.Default.UseExposureTimes = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public static string GetVersion() {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }

        private void OpenFile() {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.DefaultExt = ".csv"; // Required file extension
            fileDialog.Filter = "Csv documents|*.csv"; // Optional file extensions

            if (fileDialog.ShowDialog() == true) {
                VarStarCatalog = fileDialog.FileName;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}