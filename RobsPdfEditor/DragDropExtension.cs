﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfExtensions
{
    public static class DragDropExtension
    {
        #region ScrollOnDragDropProperty
 
        public static readonly DependencyProperty ScrollOnDragDropProperty =
            DependencyProperty.RegisterAttached("ScrollOnDragDrop",
                typeof(bool),
                typeof(DragDropExtension),
                new PropertyMetadata(false, HandleScrollOnDragDropChanged));
 
        public static bool GetScrollOnDragDrop(DependencyObject element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }
 
            return (bool)element.GetValue(ScrollOnDragDropProperty);
        }
 
        public static void SetScrollOnDragDrop(DependencyObject element, bool value)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }
 
            element.SetValue(ScrollOnDragDropProperty, value);
        }
 
        private static void HandleScrollOnDragDropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            FrameworkElement container = d as FrameworkElement;
 
            if (d == null)
            {
                Debug.Fail("Invalid type!");
                return;
            }
 
            Unsubscribe(container);
 
            if (true.Equals(e.NewValue))
            {
                Subscribe(container);
            }
        }
 
        private static void Subscribe(FrameworkElement container)
        {
            container.PreviewDragOver += OnContainerPreviewDragOver;
        }
 
        private static void OnContainerPreviewDragOver(object sender, DragEventArgs e)
        {
            FrameworkElement container = sender as FrameworkElement;

            Console.WriteLine(DateTime.Now.ToShortTimeString());

            if (container == null)
            {
                return;
            }
 
            ScrollViewer scrollViewer = (ScrollViewer)container;
 
            if (scrollViewer == null)
            {
                return;
            }
 
            double tolerance = 80;
            double horizontalPos = e.GetPosition(container).X;
            double offset = 20;

            if (horizontalPos < tolerance) // Left of visible list? 
            {
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - offset); //Scroll left 
            }
            else if (horizontalPos > container.ActualWidth - tolerance) //Right of visible list? 
            {
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + offset); //Scroll right.     
            }
        }
 
        private static void Unsubscribe(FrameworkElement container)
        {
            container.PreviewDragOver -= OnContainerPreviewDragOver;
        }
 
        public static T GetFirstVisualChild<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        return (T)child;
                    }
 
                    T childItem = GetFirstVisualChild<T>(child);
                    if (childItem != null)
                    {
                        return childItem;
                    }
                }
            }
 
            return null;
        }
 
        #endregion
    }
}
