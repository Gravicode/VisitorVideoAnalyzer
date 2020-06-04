// Copyright (C) Microsoft Corporation. All rights reserved.

using VisitorAnalyzer.Helpers.CustomVision;
using Microsoft.AI.Skills.Vision.ObjectDetectorPreview;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.Graphics.Imaging;

namespace VisitorAnalyzer
{
    /// <summary>
    /// Helper class to render object detections
    /// </summary>
    internal class BoundingBoxRenderer
    {
        private Canvas m_canvas;

        // Cache the original Rects we get for resizing purposes
        private List<Rect> m_rawRects;
        private List<Line> m_rawLines;
        private List<BoundingBox> m_rawMaskRects;
        // Pre-populate rectangles/textblocks to avoid clearing and re-creating on each frame
        private Rectangle[] m_rectangles;
        private TextBlock[] m_textBlocks;
        private Line[] m_lines;

        private Rectangle[] m_facerectangles;
        private List<Rect> m_rawfaceRects;


        private TextBlock[] m_masktextBlocks;
        /// <summary>
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="maxBoxes"></param>
        /// <param name="lineThickness"></param>
        /// <param name="colorBrush">Default Colors.SpringGreen color brush if not specified</param>
        public BoundingBoxRenderer(Canvas canvas, int maxBoxes = 50, int lineThickness = 2, SolidColorBrush colorBrush = null)
        {
            m_rawRects = new List<Rect>();

            m_rectangles = new Rectangle[maxBoxes];
            m_textBlocks = new TextBlock[maxBoxes];
            m_rawLines = new List<Line>();

            m_lines = new  Line[maxBoxes];

            //masks
            m_rawMaskRects = new List<BoundingBox>();

            m_masktextBlocks = new TextBlock[maxBoxes];

            m_facerectangles = new Rectangle[maxBoxes];
            m_rawfaceRects = new List<Rect>();

            if (colorBrush == null)
            {
                colorBrush = new SolidColorBrush(Colors.SpringGreen);
            }
            var lineBrush = new SolidColorBrush(Colors.DarkRed);
            var faceBrush = new SolidColorBrush(Colors.Yellow);
            m_canvas = canvas;
            for (int i = 0; i < maxBoxes; i++)
            {
                // Create rectangles
                m_rectangles[i] = new Rectangle();
                // Default configuration
                m_rectangles[i].Stroke = colorBrush;
                m_rectangles[i].StrokeThickness = lineThickness;
                // Hide
                m_rectangles[i].Visibility = Visibility.Collapsed;
                // Add to canvas
                m_canvas.Children.Add(m_rectangles[i]);

                // Create textblocks
                m_textBlocks[i] = new TextBlock();
                // Default configuration
                m_textBlocks[i].Foreground = colorBrush;
                m_textBlocks[i].FontSize = 18;
                // Hide
                m_textBlocks[i].Visibility = Visibility.Collapsed;
                // Add to canvas
                m_canvas.Children.Add(m_textBlocks[i]);

                // Create lines
                m_lines[i] = new  Line();
                // Default configuration
                m_lines[i].StrokeThickness = 2;
                m_lines[i].Stroke = lineBrush;
                // Hide
                m_lines[i].Visibility = Visibility.Collapsed;
                // Add to canvas
                m_canvas.Children.Add(m_lines[i]);

                //create masks label
                // Create textblocks
                m_masktextBlocks[i] = new TextBlock();
                // Default configuration
                m_masktextBlocks[i].Foreground = colorBrush;
                m_masktextBlocks[i].FontSize = 18;
                // Hide
                m_masktextBlocks[i].Visibility = Visibility.Collapsed;
                // Add to canvas
                m_canvas.Children.Add(m_masktextBlocks[i]);

                m_facerectangles[i] = new Rectangle();
                // Default configuration
                m_facerectangles[i].Stroke = faceBrush;
                m_facerectangles[i].StrokeThickness = lineThickness;
                // Hide
                m_facerectangles[i].Visibility = Visibility.Collapsed;
                // Add to canvas
                m_canvas.Children.Add(m_facerectangles[i]);
            }
        }

        /// <summary>
        /// Render bounding boxes from ObjectDetections
        /// </summary>
        /// <param name="detections"></param>
        public void Render(IReadOnlyList<ObjectDetectorResult> detections)
        {
            if (detections == null) return;
            int i = 0;
            m_rawRects.Clear();
            // Render detections up to MAX_BOXES
            for (i = 0; i < detections.Count && i < m_rectangles.Length; i++)
            {
                // Cache rect
                m_rawRects.Add(detections[i].Rect);

                // Render bounding box
                m_rectangles[i].Width = detections[i].Rect.Width * m_canvas.ActualWidth;
                m_rectangles[i].Height = detections[i].Rect.Height * m_canvas.ActualHeight;
                Canvas.SetLeft(m_rectangles[i], detections[i].Rect.X * m_canvas.ActualWidth);
                Canvas.SetTop(m_rectangles[i], detections[i].Rect.Y * m_canvas.ActualHeight);
                m_rectangles[i].Visibility = Visibility.Visible;

                // Render text label
                m_textBlocks[i].Text = detections[i].Kind.ToString();
                Canvas.SetLeft(m_textBlocks[i], detections[i].Rect.X * m_canvas.ActualWidth + 2);
                Canvas.SetTop(m_textBlocks[i], detections[i].Rect.Y * m_canvas.ActualHeight + 2);
                m_textBlocks[i].Visibility = Visibility.Visible;
            }
            // Hide all remaining boxes
            for (; i < m_rectangles.Length; i++)
            {
                // Early exit: Everything after i will already be collapsed
                if (m_rectangles[i].Visibility == Visibility.Collapsed)
                {
                    break;
                }
                m_rectangles[i].Visibility = Visibility.Collapsed;
                m_textBlocks[i].Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Resize canvas and rendered bounding boxes
        /// </summary>
        public void ResizeContent(SizeChangedEventArgs args)
        {
            // Resize rendered bboxes
            for (int i = 0; i < m_rectangles.Length && m_rectangles[i].Visibility == Visibility.Visible; i++)
            {
                // Update bounding box
                m_rectangles[i].Width = m_rawRects[i].Width * m_canvas.Width;
                m_rectangles[i].Height = m_rawRects[i].Height * m_canvas.Height;
                Canvas.SetLeft(m_rectangles[i], m_rawRects[i].X * m_canvas.Width);
                Canvas.SetTop(m_rectangles[i], m_rawRects[i].Y * m_canvas.Height);

                // Update text label
                Canvas.SetLeft(m_textBlocks[i], m_rawRects[i].X * m_canvas.Width + 2);
                Canvas.SetTop(m_textBlocks[i], m_rawRects[i].Y * m_canvas.Height + 2);

             
            }
            // Resize rendered bboxes
            for (int i = 0; i < m_lines.Length && m_lines[i].Visibility == Visibility.Visible; i++)
            {
              

                //update lines
                //m_lines[i].Width = m_rawLines[i].Width * m_canvas.ActualWidth;
                //m_lines[i].Height = m_rawLines[i].Height * m_canvas.ActualHeight;
                m_lines[i].X1 = m_rawLines[i].X1 * m_canvas.ActualWidth;
                m_lines[i].X2 = m_rawLines[i].X2 * m_canvas.ActualWidth;
                m_lines[i].Y1 = m_rawLines[i].Y1 * m_canvas.ActualHeight;
                m_lines[i].Y2 = m_rawLines[i].Y2 * m_canvas.ActualHeight;
            }
            // Resize mask label
            for (int i = 0; i < m_masktextBlocks.Length && m_masktextBlocks[i].Visibility == Visibility.Visible; i++)
            {
                // Update text label
                Canvas.SetLeft(m_masktextBlocks[i], m_rawMaskRects[i].Left * m_canvas.Width + 2);
                Canvas.SetTop(m_masktextBlocks[i], m_rawMaskRects[i].Top * m_canvas.Height + 2);
            }
            // Resize face rect
            for (int i = 0; i < m_facerectangles.Length && m_facerectangles[i].Visibility == Visibility.Visible; i++)
            {
                // Update bounding box
                m_facerectangles[i].Width = m_rawfaceRects[i].Width * m_canvas.Width;
                m_facerectangles[i].Height = m_rawfaceRects[i].Height  * m_canvas.Height;
                Canvas.SetLeft(m_facerectangles[i], m_rawfaceRects[i].X  * m_canvas.Width);
                Canvas.SetTop(m_facerectangles[i], m_rawfaceRects[i].Y * m_canvas.Height);
            }
        }
        public void RenderFaceRect(List<Rect> detections)
        {
            if (detections == null) return;
            int i = 0;
            m_rawfaceRects.Clear();
            // Render detections up to MAX_BOXES
            for (i = 0; i < detections.Count && i < m_facerectangles.Length; i++)
            {
                var rect = detections[i];
                // Cache rect
                m_rawfaceRects.Add(rect);

                // Render bounding box
                m_facerectangles[i].Width = detections[i].Width * m_canvas.ActualWidth;
                m_facerectangles[i].Height = detections[i].Height * m_canvas.ActualHeight;
                Canvas.SetLeft(m_facerectangles[i], detections[i].X * m_canvas.ActualWidth); 
                Canvas.SetTop(m_facerectangles[i], detections[i].Y * m_canvas.ActualHeight);
                m_facerectangles[i].Visibility = Visibility.Visible;

               
            }
            // Hide all remaining boxes
            for (; i < m_facerectangles.Length; i++)
            {
                // Early exit: Everything after i will already be collapsed
                if (m_facerectangles[i].Visibility == Visibility.Collapsed)
                {
                    break;
                }
                m_facerectangles[i].Visibility = Visibility.Collapsed;
               
            }
        }
        public void ClearFaceRect()
        {
            var i= 0;
            for (; i < m_facerectangles.Length; i++)
            {
                // Early exit: Everything after i will already be collapsed
                if (m_facerectangles[i].Visibility == Visibility.Collapsed)
                {
                    break;
                }
                m_facerectangles[i].Visibility = Visibility.Collapsed;

            }
        }
            public void ClearLineDistance()
        {
            int i = 0;
            for (; i < m_lines.Length; i++)
            {
                // Early exit: Everything after i will already be collapsed
                if (m_lines[i].Visibility == Visibility.Collapsed)
                {
                    break;
                }
                m_lines[i].Visibility = Visibility.Collapsed;

            }
        }
        public void DistanceLineRender(List<Line> detections)
        {
            if (detections == null) return;
            int i = 0;
            m_rawLines.Clear();
            // Render detections up to MAX_BOXES
            for (i = 0; i < detections.Count && i < m_lines.Length; i++)
            {
                // Cache rect
                m_rawLines.Add(detections[i]);

                // Render line
                //m_lines[i].Width = detections[i].Width * m_canvas.ActualWidth;
                //m_lines[i].Height = detections[i].Height * m_canvas.ActualHeight;
                m_lines[i].X1 = detections[i].X1 * m_canvas.ActualWidth;
                m_lines[i].X2 = detections[i].X2 * m_canvas.ActualWidth;
                m_lines[i].Y1 = detections[i].Y1 * m_canvas.ActualHeight;
                m_lines[i].Y2 = detections[i].Y2 * m_canvas.ActualHeight;

                //Canvas.SetLeft(m_lines[i], detections[i].X * m_canvas.ActualWidth);
                //Canvas.SetTop(m_lines[i], detections[i].Y * m_canvas.ActualHeight);
                m_lines[i].Visibility = Visibility.Visible;

               
            }
            // Hide all remaining boxes
            for (; i < m_lines.Length; i++)
            {
                // Early exit: Everything after i will already be collapsed
                if (m_lines[i].Visibility == Visibility.Collapsed)
                {
                    break;
                }
                m_lines[i].Visibility = Visibility.Collapsed;
               
            }
        }

        /// <summary>
        /// Render bounding boxes from ObjectDetections
        /// </summary>
        /// <param name="detections"></param>
        public void RenderMaskLabel(IList<Helpers.CustomVision.PredictionModel> detections)
        {
            if (detections == null) return;
            int i = 0;
            m_rawMaskRects.Clear();
            // Render detections up to MAX_BOXES
            for (i = 0; i < detections.Count && i < m_masktextBlocks.Length; i++)
            {
                // Cache rect
                m_rawMaskRects.Add(detections[i].BoundingBox);

                // Render bounding box
                //m_rectangles[i].Width = detections[i].Rect.Width * m_canvas.ActualWidth;
                //m_rectangles[i].Height = detections[i].Rect.Height * m_canvas.ActualHeight;
                //Canvas.SetLeft(m_rectangles[i], detections[i].Rect.X * m_canvas.ActualWidth);
                //Canvas.SetTop(m_rectangles[i], detections[i].Rect.Y * m_canvas.ActualHeight);
                //m_rectangles[i].Visibility = Visibility.Visible;

                // Render text label
                m_masktextBlocks[i].Text = detections[i].TagName;
                Canvas.SetLeft(m_masktextBlocks[i], detections[i].BoundingBox.Left * m_canvas.ActualWidth + 2);
                Canvas.SetTop(m_masktextBlocks[i], detections[i].BoundingBox.Top * m_canvas.ActualHeight + 2);
                m_masktextBlocks[i].Visibility = Visibility.Visible;
            }
            // Hide all remaining boxes
            for (; i < m_masktextBlocks.Length; i++)
            {
                // Early exit: Everything after i will already be collapsed
                if (m_masktextBlocks[i].Visibility == Visibility.Collapsed)
                {
                    break;
                }
                //m_rectangles[i].Visibility = Visibility.Collapsed;
                m_masktextBlocks[i].Visibility = Visibility.Collapsed;
            }
        }
        public void ClearMaskLabel()
        {
            int i = 0;
            for (; i < m_masktextBlocks.Length; i++)
            {
                // Early exit: Everything after i will already be collapsed
                if (m_masktextBlocks[i].Visibility == Visibility.Collapsed)
                {
                    break;
                }
                m_masktextBlocks[i].Visibility = Visibility.Collapsed;

            }
        }
    }
}
