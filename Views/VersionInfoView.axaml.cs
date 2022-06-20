using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VersionInfoMVVM.Views
{
    public partial class VersionInfoView : UserControl
    {
        public VersionInfoView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
