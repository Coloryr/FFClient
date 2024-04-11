using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace FFClient.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        Text1.Text = "C:\\Users\\40206\\Desktop\\output.mp4";
        Video.ClientPath = "E:\\code\\FFClient\\src\\windows\\out\\build\\x64-Debug";

        Button1.Click += Button1_Click;
        Button2.Click += Button2_Click;
    }

    private void Button2_Click(object? sender, RoutedEventArgs e)
    {
        Video.Stop();
    }

    private void Button1_Click(object? sender, RoutedEventArgs e)
    {
        Video.VideoSource = Text1.Text;
        Video.Play();
    }
}
