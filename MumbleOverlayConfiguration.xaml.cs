using CLROBS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MumbleOverlayPlugin
{
    /// <summary>
    /// Interaction logic for SampleConfiguration.xaml
    /// </summary>
    public partial class MumbleOverlayConfigurationDialog : Window
    {
        private XElement config;

        public MumbleOverlayConfigurationDialog(XElement config)
        {
            InitializeComponent();
            this.config = config;

            widthText.Text = config.GetInt("width").ToString();
            heightText.Text = config.GetInt("height").ToString();
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            Int32 width;
            Int32 height;
            if (Int32.TryParse(widthText.Text, out width) &&  Int32.TryParse(heightText.Text, out height)) {
                config.SetInt("width", width);
                config.SetInt("height", height);
                DialogResult = true;
                Close();
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
