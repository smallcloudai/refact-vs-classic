using Microsoft.VisualStudio.Imaging;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RefactAI
{
    internal class StatusBar{
        Panel panel;
        StackPanel stack;
        Brush whiteBrush;
        Brush errorBrush;
        Brush transparentBrush;

        public StatusBar(){
            stack = new StackPanel();
            stack.Width = 75.0;
            stack.Orientation = Orientation.Horizontal;
            panel = VisualTreeUtils.FindChild(Application.Current.MainWindow, childName: "StatusBarPanel") as Panel;
            whiteBrush = new SolidColorBrush(Colors.White);
            errorBrush = new SolidColorBrush(Colors.Red);
            transparentBrush = new SolidColorBrush(Colors.Transparent);
            panel.Children.Add(stack);
            ShowDefaultStatusBar();
        }

        public void ShowDefaultStatusBar(){
            stack.Children.Clear();
            stack.Background = transparentBrush;
            stack.Children.Add(CreateText("|{ Refact"));
        }

        public void ShowStatusBarError(string error){
            stack.Children.Clear();
            stack.Background = errorBrush;
            stack.Children.Add(CreateImage("debug-disconnect.png"));
            stack.Children.Add(CreateText("Refact.ai"));
            stack.ToolTip = createToolTip(text: error, stack);
        }

        public void ShowLoadingSymbol(){
            stack.Children.Clear();
            stack.Background = transparentBrush;
            var img = new CrispImage() { Moniker = KnownMonikers.Sync, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Width = 16, Height = 16 };
            stack.Children.Add(img);
            stack.Children.Add(CreateText("Refact.ai"));
        }

        ToolTip createToolTip(String text, UIElement parent){
            ToolTip toolTip = new ToolTip();
            toolTip.Content = text;
            toolTip.IsEnabled = true;
            toolTip.PlacementTarget = parent;
            toolTip.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            return toolTip;
        }

        public TextBlock CreateText(string text){
            TextBlock textBlock = new TextBlock();
            textBlock.Inlines.Add(text);
            textBlock.FontSize = 12.0;
            textBlock.Foreground = whiteBrush;
            textBlock.VerticalAlignment = VerticalAlignment.Center;
            return textBlock;
        }

        Image CreateImage(string filename){
            Image myImage = new Image();
            myImage.Height = 16;

            BitmapImage myBitmapImage = new BitmapImage();

            myBitmapImage.BeginInit();
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", filename);

            myBitmapImage.UriSource = new Uri(path);
            myBitmapImage.DecodePixelWidth = 200;
            myBitmapImage.EndInit();
            myImage.Source = myBitmapImage;
            return myImage;
        }

    }
}
