using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace FFClient.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        Text1.Text = "F:\\a\\86\\86 Eighty-Six S01E01-[1080p][BDRIP][x265.FLAC][v2] (1).mkv";
        Video.ClientPath = "E:\\code\\FFClient\\src\\windows\\out\\build\\x64-Debug\\bin\\ffclient";

        Button1.Click += Button1_Click;
        Button2.Click += Button2_Click;
        Volume.ValueChanged += Volume_ValueChanged;
    }

    private void Volume_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        Video.Volume = (int)Volume.Value;
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
