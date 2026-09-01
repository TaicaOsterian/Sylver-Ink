using SylverInk.XAML.Objects.ViewModels;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using static SylverInk.Interop.VisualUtils;
using static SylverInk.XAMLUtils.SettingsUtils;

namespace SylverInk.XAML.Objects;

/// <summary>
/// Interaction logic for ColorPicker.xaml
/// </summary>
public partial class ColorPicker : UserControl
{
    public List<SolidColorBrush> AvailableBrushes { get; } = [];


    public string? ColorTag { get; set; }
    public Brush? LastColorSelection { get; set; }
    public RichTextBox? TextTarget { get; set; }
    public ColorPickerViewModel ViewModel => (ColorPickerViewModel)DataContext;

    public ColorPicker()
    {
        DataContext = new ColorPickerViewModel();
        InitializeComponent();
    }

    private void CustomColorFinished(object? sender, EventArgs e)
    {
        if (LastColorSelection is null)
            return;

        ColorChanged(ColorTag, LastColorSelection, TextTarget);
    }

    private void CustomColorOpened(object? sender, EventArgs e)
    {
        Brush? color = ColorTag switch
        {
            "P1F" => CommonUtils.Settings.MenuForeground,
            "P1B" => CommonUtils.Settings.MenuBackground,
            "P2F" => CommonUtils.Settings.ListForeground,
            "P2B" => CommonUtils.Settings.ListBackground,
            "P3F" => CommonUtils.Settings.AccentForeground,
            "P3B" => CommonUtils.Settings.AccentBackground,
            "PT" => (Brush?)TextTarget?.Selection.GetPropertyValue(TextElement.ForegroundProperty) ?? TextTarget?.Foreground,
            _ => Brushes.Transparent
        };
        ViewModel.CustomColor = color ?? Brushes.Transparent;
        ViewModel.CustomColorCode = BytesFromBrush(ViewModel.CustomColor)[2..8];
    }

    public void Init(RichTextBox? TextTarget = null)
    {
        this.TextTarget = TextTarget;
        InitBrushes();
        InitColorGrid();
    }

    private void InitBrushes()
    {
        AvailableBrushes.Clear();

        foreach (var property in typeof(Brushes)?.GetProperties() ?? [])
        {
            if (property.GetMethod?.Invoke(null, null) is not SolidColorBrush brush)
                continue;

            if (brush.Color.A < 0xFF)
                continue;

            SolidColorBrush brushCopy = new(brush.Color);
            brushCopy.SetValue(TagProperty, Uppercase().Replace(property.Name, new MatchEvaluator(match => " " + match.Value)).Trim());
            AvailableBrushes.Add(brushCopy);
        }

        AvailableBrushes.Sort(new Comparison<SolidColorBrush>((brush1, brush2) =>
        {
            var HSV1 = HSVFromRGB(brush1);
            var HSV2 = HSVFromRGB(brush2);
            return HSV1.CompareTo(HSV2);
        }));

        InitColorGrid();
    }

    private void InitColorGrid()
    {
        var column = 3;
        var row = 0;

        ColorGrid.Children.Clear();
        ColorGrid.ColumnDefinitions.Clear();
        ColorGrid.RowDefinitions.Clear();

        for (int i = 0; i < AvailableBrushes.Count; i++)
        {
            var brush = AvailableBrushes[i];

            System.Windows.Shapes.Rectangle colorRect = new()
            {
                Fill = brush,
                Margin = new(-1),
                Stretch = Stretch.UniformToFill,
                ToolTip = brush.GetValue(TagProperty) as string
            };

            Button option = new()
            {
                Content = colorRect,
                Height = 20,
                Margin = new(2.5),
                TabIndex = i,
                Width = 20,
            };

            option.Click += (sender, _) =>
            {
                var button = (Button)sender;
                LastColorSelection = TextTarget?.Selection.IsEmpty is true ? null : ((System.Windows.Shapes.Rectangle)button.Content).Fill;
                ColorChanged(ColorTag, LastColorSelection, TextTarget);
            };

            colorRect.SetValue(ToolTipService.InitialShowDelayProperty, 250);

            ColorGrid.Children.Add(option);

            Grid.SetColumn(option, column);
            Grid.SetRow(option, row);

            column++;
            if (column >= 10)
            {
                column = 0;
                row++;
            }
        }

        for (int i = 0; i < 10; i++)
            ColorGrid.ColumnDefinitions.Add(new() { Width = new(1.0, GridUnitType.Star) });

        for (int i = 0; i < 15; i++)
            ColorGrid.RowDefinitions.Add(new() { Height = new(1.0, GridUnitType.Star) });

        System.Windows.Shapes.Rectangle rainbowRect = new()
        {
            Fill = new LinearGradientBrush([
                new(Colors.Red, 0.0),
                new(Colors.Orange, 0.167),
                new(Colors.Yellow, 0.33),
                new(Colors.Green, 0.5),
                new(Colors.Blue, 0.667),
                new(Colors.Violet, 0.833),
            ], new(0, 0), new(1, 1)),
            Margin = new(-1),
            Stretch = Stretch.UniformToFill,
            ToolTip = "Custom color..."
        };

        System.Windows.Shapes.Rectangle clearRect = new()
        {
            Fill = new LinearGradientBrush([
                new(Colors.White, 0.0),
                new(Colors.White, 0.425),
                new(Colors.Red, 0.5),
                new(Colors.White, 0.575),
                new(Colors.White, 1.0)
            ], new(0, 0), new(1, 1)),
            Margin = new(-1),
            Stretch = Stretch.UniformToFill,
            ToolTip = "Default color"
        };

        Button customOption = new()
        {
            Content = rainbowRect,
            Height = 20,
            Margin = new(2.5),
            Width = 20
        };

        Button clearOption = new()
        {
            Content = clearRect,
            Height = 20,
            Margin = new(2.5),
            Width = 20
        };

        customOption.Click += (_, _) =>
        {
            ViewModel.IsColorGridOpen = false;
            ViewModel.IsCustomSelectionOpen = true;
        };

        clearOption.Click += (_, _) =>
        {
            LastColorSelection = null;
            ColorChanged(ColorTag, null, TextTarget);
        };

        ColorGrid.Children.Add(customOption);
        ColorGrid.Children.Add(clearOption);

        Grid.SetColumn(customOption, 1);
    }

    private void NewCustomColor(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        ViewModel.CustomColorCode = box.Text.StartsWith('#') ? box.Text[1..] : box.Text;
        LastColorSelection = ViewModel.CustomColor;
    }

    private void PopupKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Escape &&
            e.Key is not Key.Enter)
            return;

        ViewModel.IsColorGridOpen = false;
        ViewModel.IsCustomSelectionOpen = false;
    }

    [GeneratedRegex(@"\p{Lu}")]
    private static partial Regex Uppercase();
}
