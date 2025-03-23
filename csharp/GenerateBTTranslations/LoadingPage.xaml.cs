using Microsoft.Maui.Controls;

namespace GenerateBTTranslations
{
    public class LoadingPage : ContentPage
    {
        public LoadingPage()
        {
            Content = new Label
            {
                Text = "Loading...",
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
        }
    }
}
