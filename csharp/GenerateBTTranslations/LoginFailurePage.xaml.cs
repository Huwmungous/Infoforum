using Microsoft.Maui.Controls;


namespace GenerateBTTranslations
{
    public class LoginFailurePage : ContentPage
    {
        public LoginFailurePage()
        {
            Content = new Label
            {
                Text = "Login failed. Please try again.",
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
        }
    }
}

