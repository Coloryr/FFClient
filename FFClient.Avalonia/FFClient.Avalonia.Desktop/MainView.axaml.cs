using Avalonia.Controls;
using Avalonia.Threading;

namespace FFClient.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        Video.VideoSource = "C:\\Users\\user\\Desktop\\movie.mp4";
        Video.ClientPath = "E:\\code\\FFClient\\src\\windows\\out\\build\\x64-Debug";

        Dispatcher.UIThread.Post(() =>
        {
            Video.Play();
        });
    }
}
