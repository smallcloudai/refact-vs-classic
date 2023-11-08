using System.Windows.Controls;

namespace RefactAI
{
    /// <summary>
    /// Interaction logic for GeneralOptions.xaml
    /// </summary>
    public partial class GeneralOptions : UserControl
    {
        public GeneralOptions()
        {
            InitializeComponent();
        }
        internal GeneralOptionPage generalOptionsPage;

        public void Initialize()
        {
            pPauseCompletion.IsChecked = General.Instance.PauseCompletion;
            pTelemetryBasic.IsChecked = General.Instance.TelemetryBasic;
            pTelemetryCodeSnippets.IsChecked = General.Instance.TelemetryCodeSnippets;

            AddressURL.Text = General.Instance.AddressURL;
            APIKey.Text = General.Instance.APIKey;
            CodeCompletionModel.Text = General.Instance.CodeCompletionModel;
            CodeCompletionModelOther.Text = General.Instance.CodeCompletionModelOther;
            CodeCompletionScratchpad.Text = General.Instance.CodeCompletionScratchpad;

            General.Instance.Save();
        }

        private void pPauseCompletion_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            General.Instance.PauseCompletion = (bool)pPauseCompletion.IsChecked;
            General.Instance.Save();
        }

        private void pPauseCompletion_Unchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            General.Instance.PauseCompletion = (bool)pPauseCompletion.IsChecked;
            General.Instance.Save();
        }

        private void pTelemetryBasic_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            General.Instance.TelemetryBasic = (bool)pTelemetryBasic.IsChecked;
            General.Instance.Save();
        }

        private void pTelemetryBasic_Unchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            General.Instance.TelemetryBasic = (bool)pTelemetryBasic.IsChecked;
            General.Instance.Save();
        }

        private void pTelemetryCodeSnippets_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            General.Instance.TelemetryCodeSnippets = (bool)pTelemetryCodeSnippets.IsChecked;
            General.Instance.Save();
        }

        private void pTelemetryCodeSnippets_Unchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            General.Instance.TelemetryCodeSnippets = (bool)pTelemetryCodeSnippets.IsChecked;
            General.Instance.Save();
        }

        private void AddressURL_textChanged(object sender, TextChangedEventArgs args)
        {
            General.Instance.AddressURL = AddressURL.Text;
            General.Instance.Save();
        }
        private void APIKey_textChanged(object sender, TextChangedEventArgs args)
        {
            General.Instance.APIKey = APIKey.Text;
            General.Instance.Save();
        }
        private void CodeCompletionModel_textChanged(object sender, TextChangedEventArgs args)
        {
            General.Instance.CodeCompletionModel = CodeCompletionModel.Text;
            General.Instance.Save();
        }
        private void CodeCompletionModelOther_textChanged(object sender, TextChangedEventArgs args)
        {
            General.Instance.CodeCompletionModelOther = CodeCompletionModelOther.Text;
            General.Instance.Save();
        }
         private void CodeCompletionScratchpad_textChanged(object sender, TextChangedEventArgs args)
        {
            General.Instance.CodeCompletionScratchpad = CodeCompletionScratchpad.Text;
            General.Instance.Save();
        }
   }
}
