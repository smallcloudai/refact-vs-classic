//This file is thanks to madskristensen https://github.com/madskristensen/ShowTheShortcut/blob/master/src/StatusbarInjector.cs

using System.Windows.Media;
using System.Windows;

namespace RefactAI
{
    internal static class VisualTreeUtils{
        //breadth first search of visual tree for DependencyObject with name childName
        public static DependencyObject FindChild(DependencyObject parent, string childName){
            if (parent == null){
                return null;
            }

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childrenCount; i++){
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement frameworkElement && frameworkElement.Name == childName)
                {
                    return frameworkElement;
                }
            }

            for (int i = 0; i < childrenCount; i++){
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                child = FindChild(child, childName);

                if (child != null){
                    return child;
                }
            }

            return null;
        }
    }
}
