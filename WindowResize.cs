public MainPage()
        {
            this.InitializeComponent();
            //CESAR CHANGE:
            /*
             * I moved this code here because:
             * - You only need to set up the Window.Current.CoreWindow.SizeChanged handler once
             *  In the window_resize event are pushing lot of event handlers to the main window.
             *  every time the user tries to resize
             */
            Window.Current.CoreWindow.SizeChanged += async (ss, ee) =>
            {
                var appView = ApplicationView.GetForCurrentView();
                appView.TryResizeView(new Size(500, 600));
                ee.Handled = true;
            };
        }
