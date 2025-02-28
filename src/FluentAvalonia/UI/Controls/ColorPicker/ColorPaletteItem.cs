﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using System;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Defines the control used in the Color Palette of a <see cref="FAColorPicker"/> to
/// preview or select a color
/// </summary>
public partial class ColorPaletteItem : Control
{
    public ColorPaletteItem()
    {
        MinWidth = 23;
        MinHeight = 23;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_helper == null)
            _helper = new ColorPaletteItemBorderRenderHelper();

        _helper.Render(context, Bounds.Size,
            _isPressed ? BorderThicknessPressed :
                IsPointerOver ? BorderThicknessPointerOver : BorderThickness,
            CornerRadius, _colorBrush,
            _isPressed ? BorderBrushPressed :
                IsPointerOver ? BorderBrushPointerOver : BorderBrush, new BoxShadows());
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPointerOverProperty)
        {
            InvalidateVisual();
        }
        else if (change.Property == ColorProperty ||
            change.Property == BorderBrushProperty ||
            change.Property == BorderThicknessProperty ||
            change.Property == CornerRadiusProperty)
        {
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPressed = true;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left &&
            e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
        {
            _isPressed = false;
            InvalidateVisual();

            var cp = this.FindAncestorOfType<FAColorPicker>();
            if (cp != null)
            {
                cp.Color = Color;
                e.Handled = true;
            }
        }
    }

    private bool _isPressed;
    private ColorPaletteItemBorderRenderHelper _helper;
    private SolidColorBrush _colorBrush;

    // Keep this separate from the /Internal/BorderRenderHelper - as this removes the
    // background in drawing and is specific to the ColorPaletteItem
    // TODO: probably can remove this later & use the normal one
    internal class ColorPaletteItemBorderRenderHelper
    {
        private bool _useComplexRendering;
        private bool? _backendSupportsIndividualCorners;
        private StreamGeometry _backgroundGeometryCache;
        private StreamGeometry _borderGeometryCache;
        private Size _size;
        private Thickness _borderThickness;
        private CornerRadius _cornerRadius;
        private bool _initialized;

        void Update(Size finalSize, Thickness borderThickness, CornerRadius cornerRadius)
        {
            _backendSupportsIndividualCorners ??= AvaloniaLocator.Current.GetService<IPlatformRenderInterface>()
                .SupportsIndividualRoundRects;
            _size = finalSize;
            _borderThickness = borderThickness;
            _cornerRadius = cornerRadius;
            _initialized = true;

            if (borderThickness.IsUniform && (cornerRadius.IsUniform || _backendSupportsIndividualCorners == true))
            {
                _backgroundGeometryCache = null;
                _borderGeometryCache = null;
                _useComplexRendering = false;
            }
            else
            {
                _useComplexRendering = true;

                var boundRect = new Rect(finalSize);
                var innerRect = boundRect.Deflate(borderThickness);
                BorderGeometryKeypoints backgroundKeypoints = null;
                StreamGeometry backgroundGeometry = null;

                if (innerRect.Width != 0 && innerRect.Height != 0)
                {
                    backgroundGeometry = new StreamGeometry();
                    backgroundKeypoints = new BorderGeometryKeypoints(innerRect, borderThickness, cornerRadius, true);

                    using (var ctx = backgroundGeometry.Open())
                    {
                        CreateGeometry(ctx, innerRect, backgroundKeypoints);
                    }

                    _backgroundGeometryCache = backgroundGeometry;
                }
                else
                {
                    _backgroundGeometryCache = null;
                }

                if (boundRect.Width != 0 && innerRect.Height != 0)
                {
                    var borderGeometryKeypoints = new BorderGeometryKeypoints(boundRect, borderThickness, cornerRadius, false);
                    var borderGeometry = new StreamGeometry();

                    using (var ctx = borderGeometry.Open())
                    {
                        CreateGeometry(ctx, boundRect, borderGeometryKeypoints);

                        if (backgroundGeometry != null)
                        {
                            CreateGeometry(ctx, innerRect, backgroundKeypoints);
                        }
                    }

                    _borderGeometryCache = borderGeometry;
                }
                else
                {
                    _borderGeometryCache = null;
                }
            }
        }

        public void Render(DrawingContext context,
            Size finalSize, Thickness borderThickness, CornerRadius cornerRadius,
            IBrush background, IBrush borderBrush, BoxShadows boxShadows)
        {
            if (_size != finalSize
                || _borderThickness != borderThickness
                || _cornerRadius != cornerRadius
                || !_initialized)
                Update(finalSize, borderThickness, cornerRadius);
            RenderCore(context, background, borderBrush, boxShadows);
        }

        void RenderCore(DrawingContext context, IBrush background, IBrush borderBrush, BoxShadows boxShadows)
        {
            if (_useComplexRendering)
            {
                var backgroundGeometry = _backgroundGeometryCache;
                if (backgroundGeometry != null)
                {
                    context.DrawGeometry(background, null, backgroundGeometry);
                }

                var borderGeometry = _borderGeometryCache;
                if (borderGeometry != null)
                {
                    context.DrawGeometry(borderBrush, null, borderGeometry);
                }
            }
            else
            {
                var borderThickness = _borderThickness.Top;
                IPen pen = null;

                if (borderThickness > 0)
                {
                    pen = new Pen(borderBrush, borderThickness);
                }

                var rect = new Rect(_size);
                if (!MathUtilities.IsZero(borderThickness))
                    rect = rect.Deflate(borderThickness * 0.5);
                var rrect = new RoundedRect(rect, _cornerRadius.TopLeft, _cornerRadius.TopRight,
                    _cornerRadius.BottomRight, _cornerRadius.BottomLeft);

                context.DrawRectangle(background, pen, rrect, boxShadows);
            }
        }

        private static void CreateGeometry(StreamGeometryContext context, Rect boundRect, BorderGeometryKeypoints keypoints)
        {
            context.BeginFigure(keypoints.TopLeft, true);

            // Top
            context.LineTo(keypoints.TopRight);

            // TopRight corner
            var radiusX = boundRect.TopRight.X - keypoints.TopRight.X;
            var radiusY = keypoints.RightTop.Y - boundRect.TopRight.Y;
            if (radiusX != 0 || radiusY != 0)
            {
                context.ArcTo(keypoints.RightTop, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise);
            }

            // Right
            context.LineTo(keypoints.RightBottom);

            // BottomRight corner
            radiusX = boundRect.BottomRight.X - keypoints.BottomRight.X;
            radiusY = boundRect.BottomRight.Y - keypoints.RightBottom.Y;
            if (radiusX != 0 || radiusY != 0)
            {
                context.ArcTo(keypoints.BottomRight, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise);
            }

            // Bottom
            context.LineTo(keypoints.BottomLeft);

            // BottomLeft corner
            radiusX = keypoints.BottomLeft.X - boundRect.BottomLeft.X;
            radiusY = boundRect.BottomLeft.Y - keypoints.LeftBottom.Y;
            if (radiusX != 0 || radiusY != 0)
            {
                context.ArcTo(keypoints.LeftBottom, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise);
            }

            // Left
            context.LineTo(keypoints.LeftTop);

            // TopLeft corner
            radiusX = keypoints.TopLeft.X - boundRect.TopLeft.X;
            radiusY = keypoints.LeftTop.Y - boundRect.TopLeft.Y;

            if (radiusX != 0 || radiusY != 0)
            {
                context.ArcTo(keypoints.TopLeft, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise);
            }

            context.EndFigure(true);
        }

        private class BorderGeometryKeypoints
        {
            internal BorderGeometryKeypoints(Rect boundRect, Thickness borderThickness, CornerRadius cornerRadius, bool inner)
            {
                var left = 0.5 * borderThickness.Left;
                var top = 0.5 * borderThickness.Top;
                var right = 0.5 * borderThickness.Right;
                var bottom = 0.5 * borderThickness.Bottom;

                double leftTopY;
                double topLeftX;
                double topRightX;
                double rightTopY;
                double rightBottomY;
                double bottomRightX;
                double bottomLeftX;
                double leftBottomY;

                if (inner)
                {
                    leftTopY = Math.Max(0, cornerRadius.TopLeft - top) + boundRect.TopLeft.Y;
                    topLeftX = Math.Max(0, cornerRadius.TopLeft - left) + boundRect.TopLeft.X;
                    topRightX = boundRect.Width - Math.Max(0, cornerRadius.TopRight - top) + boundRect.TopLeft.X;
                    rightTopY = Math.Max(0, cornerRadius.TopRight - right) + boundRect.TopLeft.Y;
                    rightBottomY = boundRect.Height - Math.Max(0, cornerRadius.BottomRight - bottom) + boundRect.TopLeft.Y;
                    bottomRightX = boundRect.Width - Math.Max(0, cornerRadius.BottomRight - right) + boundRect.TopLeft.X;
                    bottomLeftX = Math.Max(0, cornerRadius.BottomLeft - left) + boundRect.TopLeft.X;
                    leftBottomY = boundRect.Height - Math.Max(0, cornerRadius.BottomLeft - bottom) + boundRect.TopLeft.Y;
                }
                else
                {
                    leftTopY = cornerRadius.TopLeft + top + boundRect.TopLeft.Y;
                    topLeftX = cornerRadius.TopLeft + left + boundRect.TopLeft.X;
                    topRightX = boundRect.Width - (cornerRadius.TopRight + right) + boundRect.TopLeft.X;
                    rightTopY = cornerRadius.TopRight + top + boundRect.TopLeft.Y;
                    rightBottomY = boundRect.Height - (cornerRadius.BottomRight + bottom) + boundRect.TopLeft.Y;
                    bottomRightX = boundRect.Width - (cornerRadius.BottomRight + right) + boundRect.TopLeft.X;
                    bottomLeftX = cornerRadius.BottomLeft + left + boundRect.TopLeft.X;
                    leftBottomY = boundRect.Height - (cornerRadius.BottomLeft + bottom) + boundRect.TopLeft.Y;
                }

                var leftTopX = boundRect.TopLeft.X;
                var topLeftY = boundRect.TopLeft.Y;
                var topRightY = boundRect.TopLeft.Y;
                var rightTopX = boundRect.Width + boundRect.TopLeft.X;
                var rightBottomX = boundRect.Width + boundRect.TopLeft.X;
                var bottomRightY = boundRect.Height + boundRect.TopLeft.Y;
                var bottomLeftY = boundRect.Height + boundRect.TopLeft.Y;
                var leftBottomX = boundRect.TopLeft.X;

                LeftTop = new Point(leftTopX, leftTopY);
                TopLeft = new Point(topLeftX, topLeftY);
                TopRight = new Point(topRightX, topRightY);
                RightTop = new Point(rightTopX, rightTopY);
                RightBottom = new Point(rightBottomX, rightBottomY);
                BottomRight = new Point(bottomRightX, bottomRightY);
                BottomLeft = new Point(bottomLeftX, bottomLeftY);
                LeftBottom = new Point(leftBottomX, leftBottomY);

                // Fix overlap
                if (TopLeft.X > TopRight.X)
                {
                    var scaledX = topLeftX / (topLeftX + topRightX) * boundRect.Width;
                    TopLeft = new Point(scaledX, TopLeft.Y);
                    TopRight = new Point(scaledX, TopRight.Y);
                }

                if (RightTop.Y > RightBottom.Y)
                {
                    var scaledY = rightBottomY / (rightTopY + rightBottomY) * boundRect.Height;
                    RightTop = new Point(RightTop.X, scaledY);
                    RightBottom = new Point(RightBottom.X, scaledY);
                }

                if (BottomRight.X < BottomLeft.X)
                {
                    var scaledX = bottomLeftX / (bottomLeftX + bottomRightX) * boundRect.Width;
                    BottomRight = new Point(scaledX, BottomRight.Y);
                    BottomLeft = new Point(scaledX, BottomLeft.Y);
                }

                if (LeftBottom.Y < LeftTop.Y)
                {
                    var scaledY = leftTopY / (leftTopY + leftBottomY) * boundRect.Height;
                    LeftBottom = new Point(LeftBottom.X, scaledY);
                    LeftTop = new Point(LeftTop.X, scaledY);
                }
            }

            internal Point LeftTop { get; }

            internal Point TopLeft { get; }

            internal Point TopRight { get; }

            internal Point RightTop { get; }

            internal Point RightBottom { get; }

            internal Point BottomRight { get; }

            internal Point BottomLeft { get; }

            internal Point LeftBottom { get; }
        }
    }

}

