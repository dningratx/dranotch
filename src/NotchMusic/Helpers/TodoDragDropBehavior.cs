using System;
using System.Windows;
using System.Windows.Controls;
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
/// Attached behavior untuk drag-and-drop naik-turun pada item to-do list (reordering).
/// </summary>
public static class TodoDragDropBehavior
{
    private static Point _startPoint;
    private static TodoItemViewModel? _draggedItem;
    private static Border? _draggedBorder;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(TodoDragDropBehavior),
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

        // Jangan mulai drag jika yang diklik adalah CheckBox, TextBox, atau Button hapus
        var originalSource = e.OriginalSource as DependencyObject;
        if (IsInteractiveControl(originalSource))
        {
            _draggedItem = null;
            _draggedBorder = null;
            return;
        }

        _startPoint = e.GetPosition(null);
        _draggedItem = border.DataContext as TodoItemViewModel;
        _draggedBorder = border;
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
        if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null || _draggedBorder == null)
            return;

        Point currentPoint = e.GetPosition(null);
        Vector diff = currentPoint - _startPoint;

        if (Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance || Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance)
        {
            var itemToDrag = _draggedItem;
            var border = _draggedBorder;
            _draggedItem = null;
            _draggedBorder = null;

            border.Opacity = 0.5;
            try
            {
                var data = new DataObject("TodoItemViewModel", itemToDrag);
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
        if (!e.Data.GetDataPresent("TodoItemViewModel") || sender is not Border border)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var sourceItem = e.Data.GetData("TodoItemViewModel") as TodoItemViewModel;
        var targetItem = border.DataContext as TodoItemViewModel;

        if (sourceItem != null && targetItem != null && sourceItem != targetItem)
        {
            e.Effects = DragDropEffects.Move;
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2));
            border.BorderThickness = new Thickness(1.2);
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
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

        if (!e.Data.GetDataPresent("TodoItemViewModel")) return;

        var sourceItem = e.Data.GetData("TodoItemViewModel") as TodoItemViewModel;
        var targetItem = border.DataContext as TodoItemViewModel;

        if (sourceItem != null && targetItem != null && sourceItem != targetItem)
        {
            var mainVm = FindMainViewModel(border);
            if (mainVm != null)
            {
                mainVm.MoveTodo(sourceItem, targetItem);
            }
        }
        e.Handled = true;
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
