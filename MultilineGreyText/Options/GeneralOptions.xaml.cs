using System.Windows.Controls;

namespace RefactAI{

    /// <summary>
    /// Interaction logic for GeneralOptions.xaml
    /// </summary>
    public partial class GeneralOptions : UserControl {

        internal GeneralOptionPage generalOptionsPage;

        public GeneralOptions() {
            InitializeComponent();
        }

        //Sets up all of the variables to display the current settings
        public void Initialize(){
            pPauseCompletion.IsChecked = General.Instance.PauseCompletion;
            pTelemetryCodeSnippets.IsChecked = General.Instance.TelemetryCodeSnippets;

            AddressURL.Text = General.Instance.AddressURL;
            APIKey.Text = General.Instance.APIKey;
            CodeCompletionModel.Text = General.Instance.CodeCompletionModel;
            CodeCompletionModelOther.Text = General.Instance.CodeCompletionModelOther;
            CodeCompletionScratchpad.Text = General.Instance.CodeCompletionScratchpad;

            General.Instance.Save();
        }

        //pause completion checkbox checked
        private void pPauseCompletion_Checked(object sender, System.Windows.RoutedEventArgs e){
            General.Instance.PauseCompletion = (bool)pPauseCompletion.IsChecked;
            General.Instance.Save();
            NotifySettingsChanged();
        }

        //pause completion checkbox unchecked
        private void pPauseCompletion_Unchecked(object sender, System.Windows.RoutedEventArgs e){
            General.Instance.PauseCompletion = (bool)pPauseCompletion.IsChecked;
            General.Instance.Save();
            NotifySettingsChanged();
        }

        //code snippets checked
        private void pTelemetryCodeSnippets_Checked(object sender, System.Windows.RoutedEventArgs e){
            General.Instance.TelemetryCodeSnippets = (bool)pTelemetryCodeSnippets.IsChecked;
            General.Instance.Save();
            NotifySettingsChanged();
        }

        //code snippets unchecked
        private void pTelemetryCodeSnippets_Unchecked(object sender, System.Windows.RoutedEventArgs e){
            General.Instance.TelemetryCodeSnippets = (bool)pTelemetryCodeSnippets.IsChecked;
            General.Instance.Save();
            NotifySettingsChanged();
        }

        //address url text handler
        private void AddressURL_textChanged(object sender, TextChangedEventArgs args){
            General.Instance.AddressURL = AddressURL.Text;
            General.Instance.Save();
            NotifySettingsChanged();
        }

        //api key text handler
        private void APIKey_textChanged(object sender, TextChangedEventArgs args){
            General.Instance.APIKey = APIKey.Text;
            General.Instance.Save();
            NotifySettingsChanged();
        }

        //code completion model text handler
        private void CodeCompletionModel_textChanged(object sender, TextChangedEventArgs args){
            General.Instance.CodeCompletionModel = CodeCompletionModel.Text;
            General.Instance.Save();
            NotifySettingsChanged();
        }

        //code completion other text handler
        private void CodeCompletionModelOther_textChanged(object sender, TextChangedEventArgs args){
            General.Instance.CodeCompletionModelOther = CodeCompletionModelOther.Text;
            General.Instance.Save();
            NotifySettingsChanged();
        }

        //code completion scratchpad text handler
        private void CodeCompletionScratchpad_textChanged(object sender, TextChangedEventArgs args){
            General.Instance.CodeCompletionScratchpad = CodeCompletionScratchpad.Text;
            General.Instance.Save();
            NotifySettingsChanged();
        }

        // Notify settings changes
        private void NotifySettingsChanged(){
            // Implementation to notify settings changes
        }
   }
}
