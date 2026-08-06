using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using Apollo.Core;
using Apollo.Devices;
using Apollo.Enums;

namespace Apollo.DeviceViewers {
    public class UnderlightsViewer: UserControl {
        public static readonly string DeviceIdentifier = "underlights";

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            Mode = this.Get<ComboBox>("Mode");
            Bypass = this.Get<CheckBox>("Bypass");
        }

        Underlights _underlights;
        ComboBox Mode;
        CheckBox Bypass;

        public UnderlightsViewer() => new InvalidOperationException();

        public UnderlightsViewer(Underlights underlights) {
            InitializeComponent();

            _underlights = underlights;

            Mode.SelectedIndex = (int)_underlights.Mode;
            Bypass.IsChecked = _underlights.Bypass;
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) => _underlights = null;

        void Mode_Changed(object sender, SelectionChangedEventArgs e) {
            UnderlightsType selected = (UnderlightsType)Mode.SelectedIndex;

            if (_underlights.Mode != selected)
                Program.Project.Undo.AddAndExecute(new Underlights.ModeUndoEntry(
                    _underlights,
                    _underlights.Mode,
                    selected,
                    Mode.Items
                ));
        }

        public void SetMode(UnderlightsType mode) => Mode.SelectedIndex = (int)mode;

        void Bypass_Changed(object sender, RoutedEventArgs e) {
            bool value = Bypass.IsChecked.Value;

            if (_underlights.Bypass != value)
                Program.Project.Undo.AddAndExecute(new Underlights.BypassUndoEntry(
                    _underlights,
                    _underlights.Bypass,
                    value
                ));
        }

        public void SetBypass(bool value) => Bypass.IsChecked = value;
    }
}
