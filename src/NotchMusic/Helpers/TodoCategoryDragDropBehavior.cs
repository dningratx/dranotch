using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using NotchMusic.ViewModels;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using DataObject = System.Windows.DataObject;
using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;

namespace NotchMusic.Helpers;

/// <summary>
/// Attached behavior untuk drag-and-drop naik-turun pada header kategori to-do list.
/// Ketika kategori digeser, seluruh tugas di dalamnya (anak-anaknya) otomatis ikut berpindah.
/// </summary>
public static class TodoCategoryDragDropBehavior
{
    private static Point _startPoint;
    private static string? _draggedCategory;
    private static Border? _draggedBorder;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(TodoCategoryDragDropBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Border border)
        {
            if ((bool)e.NewValue)
            {
                border.AllowDrop = true;
                border.PreviewMouseLeftButtonDown += Border_PreviewMouseLeftButtonDown;
                border.PreviewMouseMove += Border_PreviewMouseMove;
                border.DragOver += Border_DragOver;
                border.DragLeave += Border_DragLeave;
                border.Drop += Border_Drop;
            }
            else
            {
                border.AllowDrop = false;
                border.PreviewMouseLeftButtonDown -= Border_PreviewMouseLeftButtonDown;
                border.PreviewMouseMove -= Border_PreviewMouseMove;
                border.DragOver -= Border_DragOver;
                border.DragLeave -= Border_DragLeave;
                border.Drop -= Border_Drop;
            }
        }
    }

    private static void Border_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border) return;

        // Jangan mulai drag jika yang diklik adalah Button (misal tombol panah up/down) atau kontrol interaktif lain
        var originalSource = e.OriginalSource as DependencyObject;
        if (IsInteractiveControl(originalSource))
        {
            _draggedCategory = null;
            _draggedBorder = null;
            return;
        }

        _startPoint = e.GetPosition(null);
        _draggedCategory = ExtractCategoryName(border.DataContext);
        _draggedBorder = border;
    }

    private static string? ExtractCategoryName(object? dataContext)
    {
        if (dataContext is CollectionViewGroup group)
        {
            return group.Name?.ToString();
        }
        return dataContext?.ToString();
    }

    private static bool IsInteractiveControl(DependencyObject? obj)
    {
        while (obj != null)
        {
            if (obj is Button || obj is CheckBox || obj is TextBox)
                return true;
            if (obj is Border b && GetIsEnabled(b))
                break;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return false;
    }

    private static void Border_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedCategory == null || _draggedBorder == null)
            return;

        Point currentPoint = e.GetPosition(null);
        Vector diff = currentPoint - _startPoint;

        if (Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance || Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance)
        {
            var categoryToDrag = _draggedCategory;
            var border = _draggedBorder;
            _draggedCategory = null;
            _draggedBorder = null;

            border.Opacity = 0.5;
            try
            {
                var data = new DataObject("TodoCategoryGroup", categoryToDrag);
                DragDrop.DoDragDrop(border, data, DragDropEffects.Move);
            }
            finally
            {
                border.Opacity = 1.0;
            }
        }
    }

    private static void Border_DragOver(object sender, DragEventArgs e)
    {
        if (sender is not Border border)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        // Handle category group drag & drop
        if (e.Data.GetDataPresent("TodoCategoryGroup"))
        {
            var sourceCat = e.Data.GetData("TodoCategoryGroup") as string;
            var targetCat = ExtractCategoryName(border.DataContext);

            if (!string.IsNullOrWhiteSpace(sourceCat) &&
                !string.IsNullOrWhiteSpace(targetCat) &&
                !string.Equals(sourceCat, targetCat, StringComparison.OrdinalIgnoreCase))
            {
                e.Effects = DragDropEffects.Move;
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2));
                border.BorderThickness = new Thickness(0, 2, 0, 0);
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
            return;
        }

        // Optional: allow dragging a single item onto a category header to reassign category
        if (e.Data.GetDataPresent("TodoItemViewModel"))
        {
            var item = e.Data.GetData("TodoItemViewModel") as TodoItemViewModel;
            var targetCat = ExtractCategoryName(border.DataContext);

            if (item != null && !string.IsNullOrWhiteSpace(targetCat))
            {
                e.Effects = DragDropEffects.Move;
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
                border.BorderThickness = new Thickness(1);
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private static void Border_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.BorderBrush = null;
            border.BorderThickness = new Thickness(0);
        }
    }

    private static void Border_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Border border) return;

        border.BorderBrush = null;
        border.BorderThickness = new Thickness(0);

        if (e.Data.GetDataPresent("TodoCategoryGroup"))
        {
            var sourceCat = e.Data.GetData("TodoCategoryGroup") as string;
            var targetCat = ExtractCategoryName(border.DataContext);

            if (!string.IsNullOrWhiteSpace(sourceCat) &&
                !string.IsNullOrWhiteSpace(targetCat) &&
                !string.Equals(sourceCat, targetCat, StringComparison.OrdinalIgnoreCase))
            {
                var mainVm = FindMainViewModel(border);
                if (mainVm != null)
                {
                    mainVm.MoveCategory(sourceCat, targetCat);
                }
            }
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent("TodoItemViewModel"))
        {
            var item = e.Data.GetData("TodoItemViewModel") as TodoItemViewModel;
            var targetCat = ExtractCategoryName(border.DataContext);

            if (item != null && !string.IsNullOrWhiteSpace(targetCat))
            {
                string newCat = string.Equals(targetCat, "Tanpa Kategori", StringComparison.OrdinalIgnoreCase) ? "" : targetCat;
                if (!string.Equals(item.Category, newCat, StringComparison.OrdinalIgnoreCase))
                {
                    item.Category = newCat;
                    var mainVm = FindMainViewModel(border);
                    mainVm?.MoveCategory(item.CategoryGroup, targetCat);
                }
            }
            e.Handled = true;
            return;
        }
    }

    private static MainViewModel? FindMainViewModel(FrameworkElement element)
    {
        DependencyObject? current = element;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is MainViewModel vm)
            {
                return vm;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return Application.Current?.MainWindow?.DataContext as MainViewModel;
    }
}
