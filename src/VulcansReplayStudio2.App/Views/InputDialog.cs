using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VulcansReplayStudio2.App.Views;

public static class InputDialog
{
    public static string? Show(string title, string prompt, string defaultValue = "")
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Black,
            Owner = Application.Current.MainWindow
        };
        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var promptText = new TextBlock { Text = prompt, Foreground = new BrushConverter().ConvertFrom("#F1F5F9") as Brush, TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(promptText, 0);
        var input = new TextBox { Text = defaultValue, Foreground = new BrushConverter().ConvertFrom("#F1F5F9") as Brush, Background = new BrushConverter().ConvertFrom("#151B25") as Brush, BorderBrush = new BrushConverter().ConvertFrom("#252D39") as Brush, Margin = new Thickness(0,8,0,0) };
        Grid.SetRow(input, 1);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,16,0,0) };
        var okBtn = new Button { Content = "OK", Width = 80, Margin = new Thickness(0,0,8,0) };
        var cancelBtn = new Button { Content = "Cancel", Width = 80 };
        okBtn.Click += (s, e) => { dialog.Close(); };
        cancelBtn.Click += (s, e) => { dialog.Close(); };
        buttons.Children.Add(okBtn);
        buttons.Children.Add(cancelBtn);
        Grid.SetRow(buttons, 2);
        grid.Children.Add(promptText);
        grid.Children.Add(input);
        grid.Children.Add(buttons);
        dialog.Content = grid;
        dialog.KeyDown += (s, e) => { if (e.Key == Key.Enter) dialog.Close(); if (e.Key == Key.Escape) { input.Text = null; dialog.Close(); } };
        input.Focus();
        var result = dialog.ShowDialog();
        return result == true ? input.Text : null;
    }
}