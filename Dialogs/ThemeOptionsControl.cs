﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace IndentGuide {
    public partial class ThemeOptionsControl : UserControl {
        private readonly IndentGuideService Service;
        private readonly IVsTextManager TextManagerService;
        private readonly IVsEditorAdaptersFactoryService EditorAdapters;

        private readonly IThemeAwareDialog Child;

        public ThemeOptionsControl(IThemeAwareDialog child) {
            InitializeComponent();

            Child = child;
            var control = child as Control;
            Debug.Assert(Child != null);
            Debug.Assert(control != null);

            tableContent.Controls.Add(control);
            tableContent.SetColumn(control, 0);
            tableContent.SetRow(control, 1);

            var provider = ServiceProvider.GlobalProvider;
            Service = provider.GetService(typeof(SIndentGuide)) as IndentGuideService;
            Child.Service = (IIndentGuide)Service;

            TextManagerService = (IVsTextManager)provider.GetService(typeof(SVsTextManager));

            var componentModel = (IComponentModel)provider.GetService(typeof(SComponentModel));
            EditorAdapters = (IVsEditorAdaptersFactoryService)componentModel
                .GetService<IVsEditorAdaptersFactoryService>();
        }

        internal void Activate() {
            try {
                IVsTextView view = null;
                IWpfTextView wpfView = null;
                TextManagerService.GetActiveView(0, null, out view);
                if (view == null) {
                    CurrentContentType = null;
                } else {
                    wpfView = EditorAdapters.GetWpfTextView(view);
                    CurrentContentType = wpfView.TextDataModel.ContentType.DisplayName;
                }
            } catch {
                CurrentContentType = null;
            }

            Child.Activate();

            IndentTheme activeTheme;
            if (CurrentContentType == null ||
                !Service.Themes.TryGetValue(CurrentContentType, out activeTheme) || 
                activeTheme == null) {
                activeTheme = Service.DefaultTheme;
            }
            if (activeTheme == null) {
                activeTheme = Service.DefaultTheme = new IndentTheme();
            }
            ActiveTheme = activeTheme;

            UpdateThemeList();
        }

        internal void Apply() {
            Child.Apply();
        }

        internal void Close() { }

        private static IndentTheme _ActiveTheme;
        protected IndentTheme ActiveTheme {
            get { return _ActiveTheme; }
            set {
                if (_ActiveTheme != value) {
                    var old = _ActiveTheme;
                    _ActiveTheme = value;
                    Child.ActiveTheme = value;
                    if (cmbTheme.SelectedItem != value && cmbTheme.Items.Contains(value))
                        cmbTheme.SelectedItem = value;
                    UpdateDisplay(_ActiveTheme, old);
                } else {
                    UpdateDisplay(_ActiveTheme, _ActiveTheme);
                }
            }
        }

        private string _CurrentContentType;
        internal string CurrentContentType {
            get { return _CurrentContentType; }
            set {
                btnCustomizeThisContentType.Text = value ?? "";
                btnCustomizeThisContentType.Visible = (value != null);
                _CurrentContentType = value;
            }
        }

        private void btnCustomizeThisContentType_Click(object sender, EventArgs e) {
            try {
                IndentTheme theme;
                if (!Service.Themes.TryGetValue(CurrentContentType, out theme)) {
                    if (ActiveTheme == null)
                        theme = new IndentTheme();
                    else
                        theme = ActiveTheme.Clone();
                    theme.ContentType = CurrentContentType;
                    Service.Themes[CurrentContentType] = theme;
                    UpdateThemeList();
                }
                cmbTheme.SelectedItem = theme;
            } catch (Exception ex) {
                Trace.WriteLine(string.Format("IndentGuide::btnCustomizeThisContentType_Click: {0}", ex));
            }
        }

        protected void UpdateThemeList() {
            try {
                cmbTheme.Items.Clear();
                cmbTheme.Items.Add(Service.DefaultTheme);
                foreach (var theme in Service.Themes.Values) {
                    cmbTheme.Items.Add(theme);
                }

                if (cmbTheme.Items.Contains(ActiveTheme)) {
                    cmbTheme.SelectedItem = ActiveTheme;
                }
            } catch (Exception ex) {
                Trace.WriteLine(string.Format("IndentGuide::UpdateThemeList: {0}", ex));
            }
        }

        protected void LoadControlStrings(IEnumerable<Control> controls) {
            try {
                foreach (var control in controls) {
                    try {
                        control.Text = ResourceLoader.LoadString(control.Name) ?? control.Text;
                    } catch (InvalidOperationException) { }

                    if (control.Controls.Count > 0) {
                        LoadControlStrings(control.Controls.OfType<Control>());
                    }
                }
            } catch (Exception ex) {
                Trace.WriteLine(string.Format("IndentGuide::LoadControlStrings: {0}", ex));
            }
        }

        private void cmbTheme_Format(object sender, ListControlConvertEventArgs e) {
            try {
                e.Value = ((IndentTheme)e.ListItem).ContentType ?? IndentTheme.DefaultThemeName;
            } catch {
                e.Value = (e.ListItem ?? "(null)").ToString();
            }
        }

        protected void UpdateDisplay() {
            UpdateDisplay(ActiveTheme, ActiveTheme);
        }

        protected void UpdateDisplay(IndentTheme active, IndentTheme previous) {
            Child.Update(active, previous);
        }

        private void ThemeOptionsControl_Load(object sender, EventArgs e) {
            LoadControlStrings(Controls.OfType<Control>());
            toolTip.SetToolTip(btnCustomizeThisContentType, ResourceLoader.LoadString("tooltipCustomizeThisContentType"));
        }

        private void cmbTheme_SelectedIndexChanged(object sender, EventArgs e) {
            ActiveTheme = cmbTheme.SelectedItem as IndentTheme;

            if (ActiveTheme != null) {
                btnThemeDelete.Enabled = true;
                btnThemeDelete.Text = ResourceLoader.LoadString(ActiveTheme.IsDefault ? "btnThemeReset" : "btnThemeDelete");
            } else {
                btnThemeDelete.Enabled = false;
                btnThemeDelete.Text = ResourceLoader.LoadString("btnThemeReset");
            }

            UpdateDisplay();
        }

        private void btnThemeDelete_Click(object sender, EventArgs e) {
            if (ActiveTheme == null) return;

            try {
                if (ActiveTheme.IsDefault) {
                    var theme = Service.DefaultTheme = new IndentTheme();
                    UpdateThemeList();
                    ActiveTheme = theme;
                } else {
                    if (Service.Themes.Remove(ActiveTheme.ContentType)) {
                        int i = cmbTheme.SelectedIndex;
                        cmbTheme.Items.Remove(ActiveTheme);
                        if (i < cmbTheme.Items.Count) cmbTheme.SelectedIndex = i;
                        else cmbTheme.SelectedIndex = cmbTheme.Items.Count - 1;
                    }
                }
            } catch (Exception ex) {
                Trace.WriteLine(string.Format("IndentGuide::btnThemeDelete_Click: {0}", ex));
            }
        }

    }
}