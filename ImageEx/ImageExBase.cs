// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// ReSharper disable MemberCanBePrivate.Global

using CommunityToolkit.WinUI;

namespace ImageEx
{
    public static class Extensions
    {
        /// <summary>
        /// Determines if a rectangle intersects with another rectangle.
        /// </summary>
        /// <param name="rect1">The first rectangle to test.</param>
        /// <param name="rect2">The second rectangle to test.</param>
        /// <returns>This method returns <see langword="true"/> if there is any intersection, otherwise <see langword="false"/>.</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntersectsWith(this Rect rect1, Rect rect2) =>
            !rect1.IsEmpty &&
            !rect2.IsEmpty &&
            rect1.Left <= rect2.Right &&
            rect1.Right >= rect2.Left &&
            rect1.Top <= rect2.Bottom &&
            rect1.Bottom >= rect2.Top;
    }

    /// <summary>
    /// Base Code for ImageEx
    /// </summary>
    [TemplateVisualState(Name = LoadingState, GroupName = CommonGroup)]
    [TemplateVisualState(Name = LoadedState, GroupName = CommonGroup)]
    [TemplateVisualState(Name = UnloadedState, GroupName = CommonGroup)]
    [TemplateVisualState(Name = FailedState, GroupName = CommonGroup)]
    [TemplatePart(Name = PartImage, Type = typeof(object))]
    public abstract partial class ImageExBase : Control, IAlphaMaskProvider
    {
        private bool _isInViewport;

        /// <summary>
        /// Image name in template
        /// </summary>
        protected const string PartImage = "Image";

        /// <summary>
        /// VisualStates name in template
        /// </summary>
        protected const string CommonGroup = "CommonStates";

        /// <summary>
        /// Loading state name in template
        /// </summary>
        protected const string LoadingState = "Loading";

        /// <summary>
        /// Loaded state name in template
        /// </summary>
        protected const string LoadedState = "Loaded";

        /// <summary>
        /// Unloaded state name in template
        /// </summary>
        protected const string UnloadedState = "Unloaded";

        /// <summary>
        /// Failed name in template
        /// </summary>
        protected const string FailedState = "Failed";

        /// <summary>
        /// Gets the backing image object
        /// </summary>
        public object Image { get; private set; }

        /// <inheritdoc/>
        public bool WaitUntilLoaded => true;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageExBase"/> class.
        /// </summary>
        protected ImageExBase()
        {
            Unloaded                 += OnUnloaded;
            EffectiveViewportChanged += OnEffectiveViewportChanged;
        }

        /// <summary>
        /// Attach image opened event handler
        /// </summary>
        /// <param name="handler">Routed Event Handler</param>
        protected void AttachImageOpened(RoutedEventHandler handler)
        {
            switch (Image)
            {
                case Image image:
                    image.ImageOpened += handler;
                    break;
                case ImageBrush brush:
                    brush.ImageOpened += handler;
                    break;
            }
        }

        /// <summary>
        /// Remove image opened handler
        /// </summary>
        /// <param name="handler">RoutedEventHandler</param>
        protected void RemoveImageOpened(RoutedEventHandler handler)
        {
            switch (Image)
            {
                case Image image:
                    image.ImageOpened -= handler;
                    break;
                case ImageBrush brush:
                    brush.ImageOpened -= handler;
                    break;
            }
        }

        /// <summary>
        /// Attach image failed event handler
        /// </summary>
        /// <param name="handler">Exception Routed Event Handler</param>
        protected void AttachImageFailed(ExceptionRoutedEventHandler handler)
        {
            switch (Image)
            {
                case Image image:
                    image.ImageFailed += handler;
                    break;
                case ImageBrush brush:
                    brush.ImageFailed += handler;
                    break;
            }
        }

        /// <summary>
        /// Remove Image Failed handler
        /// </summary>
        /// <param name="handler">Exception Routed Event Handler</param>
        protected void RemoveImageFailed(ExceptionRoutedEventHandler handler)
        {
            switch (Image)
            {
                case Image image:
                    image.ImageFailed -= handler;
                    break;
                case ImageBrush brush:
                    brush.ImageFailed -= handler;
                    break;
            }
        }

        /// <summary>
        /// Update the visual state of the control when its template is changed.
        /// </summary>
        protected override void OnApplyTemplate()
        {
            RemoveImageOpened(OnImageOpened);
            RemoveImageFailed(OnImageFailed);

            Image = GetTemplateChild(PartImage);

            IsInitialized = true;

            ImageExInitialized?.Invoke(this, EventArgs.Empty);

            if (Source == null || !EnableLazyLoading || _isInViewport)
            {
                _lazyLoadingSource = null;
                SetSource(Source);
            }
            else
            {
                _lazyLoadingSource = Source;
            }

            AttachImageOpened(OnImageOpened);
            AttachImageFailed(OnImageFailed);

            base.OnApplyTemplate();
        }

        /// <summary>
        /// Underlying <see cref="Image.ImageOpened"/> event handler.
        /// </summary>
        /// <param name="sender">Image</param>
        /// <param name="e">Event Arguments</param>
        protected virtual void OnImageOpened(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, LoadedState, true);
            ImageExOpened?.Invoke(this, new ImageExOpenedEventArgs());
        }

        /// <summary>
        /// Underlying <see cref="Image.ImageFailed"/> event handler.
        /// </summary>
        /// <param name="sender">Image</param>
        /// <param name="e">Event Arguments</param>
        protected virtual void OnImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, FailedState, true);
            ImageExFailed?.Invoke(this, new ImageExFailedEventArgs(new Exception(e.ErrorMessage)));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            EffectiveViewportChanged -= OnEffectiveViewportChanged;
        }

        private void OnEffectiveViewportChanged(FrameworkElement                  sender,
                                                EffectiveViewportChangedEventArgs args)
        {
            if (!EnableLazyLoading || !IsLoaded)
            {
                return;
            }

            double threshold = LazyLoadingThreshold;
            Rect controlRect = new(-threshold,
                                   -threshold,
                                   ActualWidth + threshold * 2,
                                   ActualHeight + threshold * 2);

            bool isInViewport = args.EffectiveViewport.IntersectsWith(controlRect);

            if (!isInViewport)
            {
                _isInViewport = false;
                return;
            }

            _isInViewport = true;
            if (_lazyLoadingSource is null)
            {
                return;
            }

            object source = _lazyLoadingSource;
            _lazyLoadingSource = null;
            SetSource(source);

            // Detach because it doesn't need to track the viewport once shown.
            EffectiveViewportChanged -= OnEffectiveViewportChanged;
        }
    }
}