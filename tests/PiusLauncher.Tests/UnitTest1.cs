using System.Collections.ObjectModel;
using System.Text.Json;
using PiusLauncher;

namespace PiusLauncher.Tests;

public sealed class LauncherItemTests
{
    [Fact]
    public void AddTarget_keeps_first_ten_targets_and_marks_group_when_slot_has_multiple_objects()
    {
        // Given: a fresh launcher slot and eleven candidate shortcuts.
        LauncherItem item = new LauncherItem(widgetIndex: 0, slotIndex: 0);

        // When: more than ten objects are added to the same slot.
        for (int index = 1; index <= 11; index++)
        {
            item.AddTarget($@"C:\Tools\App{index}.exe", $"App {index}");
        }

        // Then: only ten are retained, the slot is visibly grouped, and the first target remains the compatibility target.
        Assert.Equal(10, item.TargetCount);
        Assert.True(item.IsGroup);
        Assert.Equal(@"C:\Tools\App1.exe", item.TargetPath);
        Assert.Equal("App 1 +9", item.DisplayName);
        Assert.Equal("10개 일괄 실행", item.Subtitle);
        Assert.Contains(@"C:\Tools\App10.exe", item.ToolTip);
        Assert.DoesNotContain(@"C:\Tools\App11.exe", item.ToolTip);
    }

    [Fact]
    public void RemoveTargetAt_removes_the_selected_target_and_keeps_remaining_order()
    {
        // Given: a grouped launcher slot with three objects.
        LauncherItem item = new LauncherItem(widgetIndex: 0, slotIndex: 0);
        item.AddTarget(@"C:\Tools\App1.exe", "App 1");
        item.AddTarget(@"C:\Tools\App2.exe", "App 2");
        item.AddTarget(@"https://example.com", "Site");

        // When: the middle object is removed by its context-menu X button.
        bool removed = item.RemoveTargetAt(1);

        // Then: only that object is removed, ordering stays stable, and the slot still reports a grouped state.
        Assert.True(removed);
        Assert.Equal(2, item.TargetCount);
        Assert.Equal(new[] { @"C:\Tools\App1.exe", "https://example.com" }, item.Targets.Select(target => target.TargetPath));
        Assert.Equal(@"C:\Tools\App1.exe", item.TargetPath);
        Assert.True(item.IsGroup);
        Assert.Equal("App 1 +1", item.DisplayName);
        Assert.DoesNotContain(@"C:\Tools\App2.exe", item.ToolTip);
    }

    [Fact]
    public void SetSlotName_overrides_slot_display_without_renaming_targets()
    {
        // Given: a grouped launcher slot with its own target names.
        LauncherItem item = new LauncherItem(widgetIndex: 0, slotIndex: 0);
        item.AddTarget(@"C:\Tools\App1.exe", "App 1");
        item.AddTarget(@"https://example.com", "Site");

        // When: the slot itself is renamed from the edit-mode context menu.
        item.SetSlotName("Morning Study");

        // Then: the slot display uses that name, while the underlying target names stay intact.
        Assert.Equal("Morning Study", item.SlotName);
        Assert.Equal("Morning Study", item.DisplayName);
        Assert.Equal(new[] { "App 1", "Site" }, item.Targets.Select(target => target.DisplayName));
        Assert.Equal("2개 일괄 실행", item.Subtitle);
    }

    [Fact]
    public void Clear_removes_targets_tooltip_and_group_state_immediately()
    {
        // Given: a grouped launcher slot.
        LauncherItem item = new LauncherItem(widgetIndex: 0, slotIndex: 0);
        item.AddTarget(@"C:\Tools\App1.exe", "App 1");
        item.AddTarget(@"https://example.com", "Site");

        // When: the slot is cleared from edit mode.
        item.Clear();

        // Then: the model reports an empty slot immediately, with no stale grouped state or tooltip entries.
        Assert.Empty(item.Targets);
        Assert.Equal(0, item.TargetCount);
        Assert.False(item.HasTarget);
        Assert.False(item.IsGroup);
        Assert.Equal("슬롯 01", item.DisplayName);
        Assert.Equal("+ 눌러 추가", item.Subtitle);
        Assert.DoesNotContain("App 1", item.ToolTip);
        Assert.DoesNotContain("example.com", item.ToolTip);
    }

    [Fact]
    public void MoveContentsWithinWidget_pushes_intermediate_slots_while_preserving_slot_positions()
    {
        List<LauncherItem> widget = Enumerable.Range(0, 4)
            .Select(slotIndex => new LauncherItem(widgetIndex: 0, slotIndex))
            .ToList();
        widget[0].AddTarget(@"C:\Study\Alpha.exe", "Alpha");
        widget[0].SetSlotName("Alpha Slot");
        widget[1].AddTarget(@"C:\Study\Beta.exe", "Beta");
        widget[2].AddTarget("https://course.example", "Course");

        bool movedForward = LauncherItem.MoveContentsWithinWidget(widget, sourceSlotIndex: 0, targetSlotIndex: 2);

        Assert.True(movedForward);
        Assert.Equal(new[] { @"C:\Study\Beta.exe", "https://course.example", @"C:\Study\Alpha.exe" }, widget.Take(3).Select(item => item.TargetPath));
        Assert.Equal(new[] { 0, 1, 2, 3 }, widget.Select(item => item.SlotIndex));
        Assert.Equal("Alpha Slot", widget[2].SlotName);
        Assert.Equal("03", widget[2].SlotLabel);

        bool movedBackward = LauncherItem.MoveContentsWithinWidget(widget, sourceSlotIndex: 2, targetSlotIndex: 0);

        Assert.True(movedBackward);
        Assert.Equal(new[] { @"C:\Study\Alpha.exe", @"C:\Study\Beta.exe", "https://course.example" }, widget.Take(3).Select(item => item.TargetPath));
        Assert.Equal("Alpha Slot", widget[0].SlotName);
        Assert.False(LauncherItem.MoveContentsWithinWidget(widget, sourceSlotIndex: 0, targetSlotIndex: 0));
    }
}

public sealed class ContextMenuLayoutTests
{
    [Fact]
    public void SlotOrbIcon_fills_the_circle_and_adds_a_glass_capsule_highlight()
    {
        string source = ReadMainWindowSource();
        string slotContentMethod = ExtractMethod(source, "UIElement CreateSlotContent");

        Assert.Contains("SlotOrbSize = 50.0", source);
        Assert.Contains("SlotOrbIconSize = 42.0", source);
        Assert.Contains("SlotOrbGlassCapsuleWidth = 28.0", source);
        Assert.Contains("SlotOrbGlassCapsuleHeight = 12.0", source);
        Assert.Contains("CreateSlotOrbGlassCapsule()", slotContentMethod);
        Assert.Contains("CreateSlotOrbIconChrome(item)", slotContentMethod);

        string chromeMethod = ExtractMethod(source, "Border CreateSlotOrbIconChrome");
        Assert.Contains("CornerRadius = new CornerRadius(SlotOrbIconSize / 2.0)", chromeMethod);
        Assert.Contains("Clip = new EllipseGeometry(new Rect(0.0, 0.0, SlotOrbIconSize, SlotOrbIconSize))", chromeMethod);
        Assert.Contains("Width = SlotOrbIconSize", chromeMethod);
        Assert.Contains("Height = SlotOrbIconSize", chromeMethod);
        Assert.Contains("Stretch = Stretch.Fill", chromeMethod);
        Assert.DoesNotContain("Width = 28.0", chromeMethod);
        Assert.DoesNotContain("Height = 28.0", chromeMethod);

        string capsuleMethod = ExtractMethod(source, "FrameworkElement CreateSlotOrbGlassCapsule");
        Assert.Contains("Panel.ZIndexProperty", capsuleMethod);
        Assert.Contains("IsHitTestVisible = false", capsuleMethod);
        Assert.Contains("RadialGradientBrush", capsuleMethod);
        Assert.Contains("LinearGradientBrush", capsuleMethod);
        Assert.Contains("Opacity = 0.78", capsuleMethod);
    }

    [Fact]
    public void SlotOrbGlassCapsule_uses_two_thirds_light_scale()
    {
        string source = ReadMainWindowSource();
        string capsuleMethod = ExtractMethod(source, "FrameworkElement CreateSlotOrbGlassCapsule");

        Assert.Contains("SlotOrbGlassCapsuleWidth = 28.0", source);
        Assert.Contains("SlotOrbGlassCapsuleHeight = 12.0", source);
        Assert.Contains("Width = SlotOrbGlassCapsuleWidth", capsuleMethod);
        Assert.Contains("Height = SlotOrbGlassCapsuleHeight", capsuleMethod);
        Assert.Contains("CornerRadius = new CornerRadius(SlotOrbGlassCapsuleHeight / 2.0)", capsuleMethod);
        Assert.DoesNotContain("Width = 42.0", capsuleMethod);
        Assert.DoesNotContain("Height = 18.0", capsuleMethod);
    }

    [Fact]
    public void EditSlotContextMenu_uses_compact_two_thirds_metrics_for_grouped_slot()
    {
        string source = ReadMainWindowSource();

        Assert.Contains("SlotContextMenuMinWidth = 190.0", source);
        Assert.Contains("SlotContextMenuOuterPadding = 5.0", source);
        Assert.Contains("SlotMenuHeaderWidth = 175.0", source);
        Assert.Contains("SlotMenuTargetRowWidth = 170.0", source);
        Assert.Contains("SlotMenuTargetGridWidth = 156.0", source);
        Assert.Contains("SlotMenuActionFontSize = 10.5", source);
        Assert.Contains("SlotMenuTargetTitleFontSize = 10.5", source);
        Assert.Contains("SlotMenuTargetPathFontSize = 9.0", source);
        Assert.Contains("SlotMenuHeaderSubtitleFontSize = 9.5", source);
        Assert.Contains("SlotMenuBadgeFontSize = 12.0", source);

        string menuHeaderMethod = ExtractMethod(source, "CreateSlotMenuHeader");
        string actionMethod = ExtractMethod(source, "CreateSlotActionMenuItem");
        string itemsHeaderMethod = ExtractMethod(source, "CreateSlotItemsHeader");
        string targetMethod = ExtractMethod(source, "CreateSlotTargetMenuItem");
        Assert.Contains("FontSize = SlotMenuHeaderTitleFontSize", menuHeaderMethod);
        Assert.Contains("FontSize = SlotMenuHeaderSubtitleFontSize", menuHeaderMethod);
        Assert.Contains("FontSize = SlotMenuBadgeFontSize", menuHeaderMethod);
        Assert.DoesNotContain("FontSize = 9.5", menuHeaderMethod);
        Assert.DoesNotContain("FontSize = 12.0", menuHeaderMethod);
        Assert.Contains("FontSize = SlotMenuActionFontSize", actionMethod);
        Assert.DoesNotContain("FontSize = 13.0", actionMethod);
        Assert.Contains("FontSize = SlotMenuItemsHeaderFontSize", itemsHeaderMethod);
        Assert.DoesNotContain("FontSize = 12.0", itemsHeaderMethod);
        Assert.Contains("FontSize = SlotMenuTargetTitleFontSize", targetMethod);
        Assert.Contains("FontSize = SlotMenuTargetPathFontSize", targetMethod);
        Assert.DoesNotContain("FontSize = 11.0", targetMethod);
    }

    [Fact]
    public void EditSlotContextMenu_keeps_ten_targets_compact_and_usable()
    {
        string source = ReadMainWindowSource();

        Assert.Contains("MaxTargetsPerSlot = 10", ReadLauncherItemSource());
        Assert.Contains("최대 10개까지", source);
        Assert.Contains("SlotMenuActionMinHeight = 24.0", source);
        Assert.Contains("SlotMenuTargetRowMinHeight = 34.0", source);
        Assert.Contains("SlotMenuDeleteButtonSize = 18.0", source);
        Assert.Contains("SlotMenuSeparatorVerticalMargin = 4.0", source);
        Assert.Contains("SlotMenuHeaderTitleFontSize = 12.0", source);
        Assert.Contains("SlotMenuItemsHeaderFontSize = 10.0", source);
    }

    [Fact]
    public void EditSlotContextMenu_clear_slot_action_closes_or_refreshes_the_open_menu()
    {
        string source = ReadMainWindowSource();
        string rebuildMethod = ExtractMethod(source, "void RebuildSlotContextMenuItems");
        string clearMethod = ExtractMethod(source, "void ClearItem");

        Assert.Contains("ClearItem(contextMenu, item)", rebuildMethod);
        Assert.DoesNotContain("ClearItem(item)", rebuildMethod);
        Assert.Contains("RebuildSlotContextMenuItems(contextMenu, item)", clearMethod);
        Assert.Contains("contextMenu.IsOpen = false", clearMethod);
    }

    [Fact]
    public void EditSlotContextMenu_mouse_leave_uses_bubble_pop_close_animation()
    {
        string source = ReadMainWindowSource();
        string createMethod = ExtractMethod(source, "ContextMenu CreateSlotContextMenu");

        Assert.Contains("SlotContextMenuBubblePopMilliseconds = 180", source);
        Assert.Contains("SlotContextMenuBubblePopPollMilliseconds = 70", source);
        Assert.Contains("SlotContextMenuBubblePopScale = 0.18", source);
        Assert.Contains("ConfigureSlotContextMenuBubblePop(contextMenu)", createMethod);

        string configureMethod = ExtractMethod(source, "void ConfigureSlotContextMenuBubblePop");
        string bubblePopMethod = ExtractMethod(source, "void BeginSlotContextMenuBubblePop");
        string pointerMethod = ExtractMethod(source, "bool IsPointerInsideSlotContextMenu");

        Assert.Contains("DispatcherTimer", configureMethod);
        Assert.Contains("pointerWasInside", configureMethod);
        Assert.Contains("pollTimer.Tick +=", configureMethod);
        Assert.Contains("IsPointerInsideSlotContextMenu(contextMenu)", configureMethod);
        Assert.Contains("contextMenu.MouseLeave +=", configureMethod);
        Assert.Contains("BeginSlotContextMenuBubblePop(contextMenu)", configureMethod);
        Assert.Contains("contextMenu.Opened +=", configureMethod);
        Assert.Contains("contextMenu.Closed +=", configureMethod);
        Assert.Contains("pollTimer.Start()", configureMethod);
        Assert.Contains("pollTimer.Stop()", configureMethod);
        Assert.Contains("System.Windows.Forms.Cursor.Position", pointerMethod);
        Assert.Contains("contextMenu.PointFromScreen", pointerMethod);
        Assert.Contains("menuBounds.Contains(localPoint)", pointerMethod);
        Assert.Contains("BackEase", bubblePopMethod);
        Assert.Contains("UIElement.OpacityProperty", bubblePopMethod);
        Assert.Contains("ScaleTransform.ScaleXProperty", bubblePopMethod);
        Assert.Contains("ScaleTransform.ScaleYProperty", bubblePopMethod);
        Assert.Contains("contextMenu.IsOpen = false", bubblePopMethod);
    }

    [Fact]
    public void Header_buttons_are_rounded_and_settings_button_is_readable_gear()
    {
        string source = ReadMainWindowSource();
        string layoutMethod = ExtractMethod(source, "Grid CreateMainLayout");

        Assert.Contains("WidgetTabWidth = 154.0", source);
        Assert.Contains("RenameButtonWidth = 77.0", source);
        Assert.Contains("RenameButtonHeight = 25.0", source);
        Assert.Contains("HeaderActionButtonWidth = 116.0", source);
        Assert.Contains("HeaderActionButtonHeight = 38.0", source);
        Assert.Contains("SettingsButtonWidth = 70.0", source);
        Assert.Contains("SettingsButtonHeight = 58.0", source);
        Assert.Contains("SettingsButtonMargin = new Thickness(64.5, 3.0, 0.0, 0.0)", source);
        Assert.Contains("Grid.SetColumnSpan(stackPanel, 2)", layoutMethod);
        Assert.Contains("CreateHeaderActionButton(AppText.Choose(\"이름 바꾸기\", \"Rename\"), RenameButtonWidth, RenameButtonHeight)", layoutMethod);
        Assert.Contains("new Thickness(18.0, 10.5, 12.0, 0.0)", layoutMethod);
        Assert.Contains("CreateSettingsButton()", layoutMethod);
        Assert.Contains("stackPanel2.Children.Add(settingsButton)", layoutMethod);
        Assert.True(
            layoutMethod.IndexOf("stackPanel2.Children.Add(button2)", StringComparison.Ordinal) <
            layoutMethod.IndexOf("stackPanel2.Children.Add(settingsButton)", StringComparison.Ordinal),
            "Expected the settings button to be added after the three widget tab buttons.");

        string buttonMethod = ExtractMethod(source, "Button CreateSettingsButton");
        Assert.Contains("Width = SettingsButtonWidth", buttonMethod);
        Assert.Contains("Height = SettingsButtonHeight", buttonMethod);
        Assert.Contains("Content = CreateSettingsGearContent()", buttonMethod);
        Assert.Contains("BorderThickness = new Thickness(0.0)", buttonMethod);
        Assert.Contains("ShowSettingsDialog()", buttonMethod);
        Assert.Contains("DropShadowEffect", buttonMethod);
        Assert.Contains("Opacity = 0.36", buttonMethod);
        Assert.Contains("Color = System.Windows.Media.Color.FromRgb(112, 52, 159)", buttonMethod);
        Assert.Contains("AutomationProperties.SetName(button, AppText.Choose(\"설정\", \"Settings\"))", buttonMethod);
        Assert.Contains("AutomationProperties.SetAutomationId(button, \"settings-button\")", buttonMethod);
        Assert.DoesNotContain("Color = System.Windows.Media.Color.FromRgb(88, 28, 135)", buttonMethod);
        Assert.DoesNotContain("34, 211, 238", buttonMethod);

        string gearMethod = ExtractMethod(source, "UIElement CreateSettingsGearContent");
        Assert.DoesNotContain("Text = \"\\uE713\"", gearMethod);
        Assert.DoesNotContain("Segoe MDL2 Assets", gearMethod);
        Assert.DoesNotContain("gear.Children.Add(glyph)", gearMethod);
        Assert.Contains("Text = AppText.Choose(\"설정\", \"Settings\")", gearMethod);
        Assert.Contains("FontSize = AppText.IsEnglish ? 10.0 : 11.0", gearMethod);
        Assert.Contains("new CornerRadius(18.0)", gearMethod);
        Assert.Contains("System.Windows.Media.Color.FromArgb(246, 39, 47, 66)", gearMethod);
        Assert.Contains("System.Windows.Media.Color.FromArgb(244, 54, 65, 83)", gearMethod);
        Assert.Contains("System.Windows.Media.Color.FromArgb(238, 112, 52, 159)", gearMethod);
        Assert.Contains("System.Windows.Media.Color.FromArgb(224, 26, 30, 47)", gearMethod);
        Assert.Contains("System.Windows.Media.Color.FromArgb(96, 223, 234, 255)", gearMethod);
        Assert.DoesNotContain("250, 204, 21", gearMethod);
        Assert.DoesNotContain("34, 211, 238", gearMethod);

        string headerButtonMethod = ExtractMethod(source, "Button CreateHeaderActionButton");
        Assert.Contains("Template = CreateRoundedButtonTemplate()", headerButtonMethod);
        Assert.Contains("new CornerRadius(10.0)", ExtractMethod(source, "ControlTemplate CreateRoundedButtonTemplate"));

        string editButtonMethod = ExtractMethod(source, "Button CreateEditModeButton");
        Assert.Contains("CreateHeaderActionButton(AppText.Choose(\"앱 수정\", \"Edit Apps\"), HeaderActionButtonWidth, HeaderActionButtonHeight)", editButtonMethod);
        Assert.Contains("new Thickness(0.0, 4.0, 0.0, 0.0)", editButtonMethod);

        string tabMethod = ExtractMethod(source, "Button CreateTabButton");
        Assert.Contains("button.Width = WidgetTabWidth", tabMethod);
        Assert.DoesNotContain("button.Width = 174.0", tabMethod);
    }

    [Fact]
    public void Settings_dialog_contains_two_polished_save_load_actions()
    {
        string source = ReadMainWindowSource();
        string dialogMethod = ExtractMethod(source, "void ShowSettingsDialog");
        string actionMethod = ExtractMethod(source, "Button CreateSettingsDialogActionButton");

        Assert.Contains("SettingsDialogWidth = 360.0", source);
        Assert.Contains("SettingsDialogHeight = 470.0", source);
        Assert.Contains("\"현재 상태 저장\"", dialogMethod);
        Assert.Contains("\"저장된 설정 불러오기\"", dialogMethod);
        Assert.Contains("SaveCurrentSettingsSnapshot(snapshotFile)", dialogMethod);
        Assert.Contains("LoadSavedSettingsSnapshot(snapshotFile)", dialogMethod);
        Assert.Contains("Microsoft.Win32.SaveFileDialog", source);
        Assert.Contains("Microsoft.Win32.OpenFileDialog", source);
        Assert.Contains("CornerRadius = new CornerRadius(18.0)", dialogMethod);
        Assert.Contains("LinearGradientBrush", dialogMethod);
        Assert.Contains("CreateSettingsDialogActionButton", dialogMethod);
        Assert.Contains("Height = 48.0", actionMethod);
        Assert.Contains("FontSize = 14.0", actionMethod);
        Assert.Contains("Padding = new Thickness(16.0, 0.0, 16.0, 0.0)", actionMethod);
        Assert.Contains("DropShadowEffect", actionMethod);
        Assert.Contains("AutomationProperties.SetName(button, text)", actionMethod);
    }

    [Fact]
    public void Settings_dialog_has_top_right_close_button_and_uses_json_file_dialogs()
    {
        string source = ReadMainWindowSource();
        string dialogMethod = ExtractMethod(source, "void ShowSettingsDialog");
        string closeMethod = ExtractMethod(source, "Button CreateSettingsDialogCloseButton");

        // A close (X) button is placed at the top-right of the settings dialog and closes it.
        Assert.Contains("CreateSettingsDialogCloseButton(dialog)", dialogMethod);
        Assert.Contains("HorizontalAlignment = System.Windows.HorizontalAlignment.Right", closeMethod);
        Assert.Contains("VerticalAlignment = VerticalAlignment.Top", closeMethod);
        Assert.Contains("dialog.Close()", closeMethod);
        Assert.Contains("AutomationProperties.SetName(button, AppText.Choose(\"닫기\", \"Close\"))", closeMethod);

        Assert.Contains("ShowSettingsFileDialogWithAppVisible(dialog, ShowSaveSettingsFileDialog)", dialogMethod);
        Assert.Contains("ShowSettingsFileDialogWithAppVisible(dialog, ShowOpenSettingsFileDialog)", dialogMethod);
        Assert.Contains("SaveCurrentSettingsSnapshot(snapshotFile)", dialogMethod);
        Assert.Contains("LoadSavedSettingsSnapshot(snapshotFile)", dialogMethod);
        Assert.DoesNotContain("OpenSavedSettingsFolder()", dialogMethod);

        string saveDialogMethod = ExtractMethod(source, "string ShowSaveSettingsFileDialog");
        string openDialogMethod = ExtractMethod(source, "string ShowOpenSettingsFileDialog");
        Assert.Contains("Microsoft.Win32.SaveFileDialog", saveDialogMethod);
        Assert.Contains("Microsoft.Win32.OpenFileDialog", openDialogMethod);
        Assert.Contains("DefaultExt = \".json\"", saveDialogMethod);
        Assert.Contains("DefaultExt = \".json\"", openDialogMethod);
        Assert.Contains("Filter = AppText.Choose(\"JSON 설정 파일 (*.json)|*.json|모든 파일 (*.*)|*.*\"", saveDialogMethod);
        Assert.Contains("Filter = AppText.Choose(\"JSON 설정 파일 (*.json)|*.json|모든 파일 (*.*)|*.*\"", openDialogMethod);
        Assert.Contains("ShowDialog(this)", saveDialogMethod);
        Assert.Contains("ShowDialog(this)", openDialogMethod);
    }

    [Fact]
    public void Settings_dialog_closes_after_successful_save_load_and_shows_app_during_file_dialogs()
    {
        string source = ReadMainWindowSource();
        string dialogMethod = ExtractMethod(source, "void ShowSettingsDialog");
        string showDialogMethod = ExtractMethod(source, "string ShowSettingsFileDialogWithAppVisible");
        string showAppMethod = ExtractMethod(source, "void ShowAppWindowForSettingsOperation");
        string restoreDialogMethod = ExtractMethod(source, "void RestoreSettingsDialogAfterCanceledOrFailedOperation");

        Assert.Contains("ShowAppWindowForSettingsOperation()", dialogMethod);
        Assert.Contains("ShowSettingsFileDialogWithAppVisible(dialog, ShowSaveSettingsFileDialog)", dialogMethod);
        Assert.Contains("ShowSettingsFileDialogWithAppVisible(dialog, ShowOpenSettingsFileDialog)", dialogMethod);
        Assert.Equal(3, CountOccurrences(dialogMethod, "CloseSettingsDialogAfterSuccessfulOperation(dialog)"));
        Assert.Contains("statusText.Text = AppText.Choose(\"저장 취소\", \"Save canceled\")", dialogMethod);
        Assert.Contains("statusText.Text = AppText.Choose(\"불러오기 취소\", \"Load canceled\")", dialogMethod);
        Assert.Contains("statusText.Text = AppText.Choose(\"저장할 수 없습니다\", \"Could not save\")", dialogMethod);
        Assert.Contains("statusText.Text = AppText.Choose(\"불러올 수 없습니다\", \"Could not load\")", dialogMethod);
        Assert.DoesNotContain("settingsDialog.Hide()", showDialogMethod);
        Assert.Contains("ShowAppWindowForSettingsOperation()", showDialogMethod);
        Assert.Contains("showFileDialog()", showDialogMethod);
        Assert.Contains("Show()", showAppMethod);
        Assert.Contains("WindowState.Normal", showAppMethod);
        Assert.Contains("Activate()", showAppMethod);
        // Restore must not call Show() (the dialog is never hidden, and Show() on a closed window throws);
        // it only re-activates the still-visible settings dialog.
        Assert.DoesNotContain("settingsDialog.Show()", restoreDialogMethod);
        Assert.Contains("settingsDialog.IsVisible", restoreDialogMethod);
        Assert.Contains("settingsDialog.Activate()", restoreDialogMethod);
    }

    [Fact]
    public void Settings_dialog_suppresses_auto_hide_and_drops_bottom_saved_settings_label()
    {
        string source = ReadMainWindowSource();
        string dialogMethod = ExtractMethod(source, "void ShowSettingsDialog");
        string tickMethod = ExtractMethod(source, "void HoverHideTimer_Tick");
        string hideMethod = ExtractMethod(source, "void BeginHideToTaskbarAnimation");
        string beginMethod = ExtractMethod(source, "void BeginSettingsInteraction");
        string endMethod = ExtractMethod(source, "void EndSettingsInteraction");
        string showDialogMethod = ExtractMethod(source, "string ShowSettingsFileDialogWithAppVisible");

        // The settings dialog no longer shows the saved-settings folder name at the bottom;
        // the status line only appears once an operation reports something.
        Assert.DoesNotContain("Text = LauncherStore.SavedSettingsFolderName", dialogMethod);
        Assert.Contains("Visibility = Visibility.Collapsed", dialogMethod);
        Assert.Equal(12, CountOccurrences(dialogMethod, "statusText.Visibility = Visibility.Visible"));

        // While the settings dialog (or one of its file dialogs) is open, the pointer leaving the
        // launcher must not auto-hide the app or the settings dialog.
        Assert.Contains("BeginSettingsInteraction()", dialogMethod);
        Assert.Contains("EndSettingsInteraction()", dialogMethod);
        Assert.Contains("_isSettingsInteractionActive = true", beginMethod);
        Assert.Contains("_isSettingsInteractionActive = false", endMethod);
        Assert.Contains("_hoverHideTimer.Start()", endMethod);
        Assert.Contains("_isEditMode || _isTrayMenuOpen || _isSettingsInteractionActive", tickMethod);
        Assert.Contains("!_isSettingsInteractionActive && !_isHideAnimationRunning", hideMethod);

        // Save/load keep the settings dialog on screen and still run the file dialog.
        Assert.DoesNotContain("settingsDialog.Hide()", showDialogMethod);
        Assert.Contains("settingsDialog.Activate()", showDialogMethod);
        Assert.Contains("showFileDialog()", showDialogMethod);
    }

    [Fact]
    public void Settings_dialog_reset_button_restores_initial_state_after_confirmation()
    {
        string source = ReadMainWindowSource();
        string dialogMethod = ExtractMethod(source, "void ShowSettingsDialog");
        string resetMethod = ExtractMethod(source, "void ResetLauncherStateToDefaults");

        // The settings dialog offers a third action that resets the launcher to its initial
        // state, guarded by a Yes/No confirmation, with the same status/close flow as save/load.
        Assert.Contains("CreateSettingsDialogActionButton(AppText.Choose(\"초기화\", \"Reset All\"))", dialogMethod);
        Assert.Contains("actions.Children.Add(resetButton)", dialogMethod);
        Assert.Contains("MessageBoxButton.YesNo", dialogMethod);
        Assert.Contains("ResetLauncherStateToDefaults()", dialogMethod);
        Assert.Contains("statusText.Text = AppText.Choose(\"초기화 취소\", \"Reset canceled\")", dialogMethod);
        Assert.Contains("statusText.Text = AppText.Choose(\"초기화 완료\", \"Reset complete\")", dialogMethod);
        Assert.Contains("statusText.Text = AppText.Choose(\"초기화할 수 없습니다\", \"Could not reset\")", dialogMethod);

        // Reset clears every slot and slot name, restores the default title and widget names,
        // selects the first widget, and persists the cleared state.
        Assert.Contains("item.Clear()", resetMethod);
        Assert.Contains("item.SetSlotName(null)", resetMethod);
        Assert.Contains("LauncherTitle = LauncherStore.DefaultLauncherTitle", resetMethod);
        Assert.Contains("tab.Name = string.Empty", resetMethod);
        Assert.Contains("SelectWidget(0)", resetMethod);
        Assert.Contains("PersistLauncherState()", resetMethod);
        Assert.Contains("UpdateTrayLabels()", resetMethod);
    }

    [Fact]
    public void Widget_tabs_switch_on_hover_without_click()
    {
        string source = ReadMainWindowSource();
        string tabMethod = ExtractMethod(source, "Button CreateTabButton");

        // Hovering a widget tab selects that widget without a click (MouseEnter -> SelectWidget),
        // guarded so re-entering the already-active tab does nothing.
        Assert.Contains("button.MouseEnter += delegate", tabMethod);
        Assert.Contains("_selectedWidgetIndex != tab.Index", tabMethod);
        Assert.Contains("SelectWidget(tab.Index)", tabMethod);

        // A stable automation id so the hover target can be located regardless of the tab's
        // (renameable) display name; the tab buttons previously exposed no automation name.
        Assert.Contains("AutomationProperties.SetAutomationId(button, \"widget-tab-\" + tab.Index)", tabMethod);

        // The existing click-to-select behavior is preserved.
        Assert.Contains("button.Click += delegate", tabMethod);
    }

    [Fact]
    public void Settings_dialog_has_per_widget_reset_buttons_arranged_in_a_row()
    {
        string source = ReadMainWindowSource();
        string dialogMethod = ExtractMethod(source, "void ShowSettingsDialog");
        string widgetResetMethod = ExtractMethod(source, "void ResetWidgetToDefaults");
        string widgetButtonMethod = ExtractMethod(source, "Button CreateWidgetResetButton");

        // A labeled per-widget reset section with three buttons (위젯 1/2/3) laid out in a single row.
        Assert.Contains("\"위젯별 초기화\"", dialogMethod);
        Assert.Contains("UniformGrid", dialogMethod);
        Assert.Contains("Columns = 3", dialogMethod);
        Assert.Contains("CreateWidgetResetButton(AppText.DefaultWidgetName(widgetIndex))", dialogMethod);
        Assert.Contains("ResetWidgetToDefaults(targetWidgetIndex)", dialogMethod);
        Assert.Contains("actions.Children.Add(widgetResetRow)", dialogMethod);

        // Each per-widget reset is confirmed (Yes/No) and keeps the dialog open on success so several
        // widgets can be reset in a row (no CloseSettingsDialogAfterSuccessfulOperation for them).
        Assert.Contains("MessageBoxButton.YesNo", dialogMethod);

        // ResetWidgetToDefaults clears only the target widget's slots/slot-names and its tab name,
        // then refreshes and persists — without touching the launcher title (that is global-only).
        Assert.Contains("_widgets[widgetIndex]", widgetResetMethod);
        Assert.Contains("item.Clear()", widgetResetMethod);
        Assert.Contains("item.SetSlotName(null)", widgetResetMethod);
        Assert.Contains("WidgetTabs[widgetIndex].Name = string.Empty", widgetResetMethod);
        Assert.Contains("PersistLauncherState()", widgetResetMethod);
        Assert.DoesNotContain("LauncherTitle = LauncherStore.DefaultLauncherTitle", widgetResetMethod);

        // The per-widget buttons are compact (height 42) and named for automation/QA.
        Assert.Contains("Height = 42.0", widgetButtonMethod);
        Assert.Contains("AutomationProperties.SetName(button, text)", widgetButtonMethod);
    }

    [Fact]
    public void Settings_dialog_language_selector_persists_and_refreshes_every_live_surface()
    {
        string source = ReadMainWindowSource();
        string dialogMethod = ExtractMethod(source, "void ShowSettingsDialog");
        string changeMethod = ExtractMethod(source, "bool ChangeLanguage");
        string refreshMethod = ExtractMethod(source, "void RefreshLocalizedInterface");
        string languageButtonMethod = ExtractMethod(source, "Button CreateLanguageChoiceButton");

        Assert.Contains("Text = AppText.Choose(\"언어\", \"Language\")", dialogMethod);
        Assert.Contains("CreateLanguageChoiceButton(\"한글\", UiLanguage.Korean)", dialogMethod);
        Assert.Contains("CreateLanguageChoiceButton(\"English\", UiLanguage.English)", dialogMethod);
        Assert.Contains("ChangeLanguage(UiLanguage.Korean)", dialogMethod);
        Assert.Contains("ChangeLanguage(UiLanguage.English)", dialogMethod);
        Assert.Contains("language-ko", languageButtonMethod);
        Assert.Contains("language-en", languageButtonMethod);

        Assert.Contains("_preferencesStore.SaveLanguage(normalizedLanguage)", changeMethod);
        Assert.Contains("AppText.IsDefaultWidgetName", changeMethod);
        Assert.Contains("RefreshLocalizedInterface()", changeMethod);
        Assert.Contains("PersistLauncherState()", changeMethod);

        Assert.Contains("UpdateEditModeButton()", refreshMethod);
        Assert.Contains("RefreshTabs()", refreshMethod);
        Assert.Contains("RefreshSlots()", refreshMethod);
        Assert.Contains("RefreshStatus()", refreshMethod);
        Assert.Contains("UpdateTrayLabels()", refreshMethod);
        Assert.Contains("CreateSettingsGearContent()", refreshMethod);
    }

    [Fact]
    public void EditMode_slots_use_custom_pointer_drag_reorder_with_push_aside_animation()
    {
        string source = ReadMainWindowSource();
        string createButtonMethod = ExtractMethod(source, "Button CreateSlotButton");
        string mouseMoveMethod = ExtractMethod(source, "void SlotButton_PreviewMouseMove");
        string mouseUpMethod = ExtractMethod(source, "void SlotButton_PreviewMouseLeftButtonUp");
        string beginDragMethod = ExtractMethod(source, "void BeginSlotDrag");
        string updateDragMethod = ExtractMethod(source, "void UpdateSlotDrag");
        string previewMethod = ExtractMethod(source, "void UpdateSlotReorderPreview");
        string pushMethod = ExtractMethod(source, "void AnimateSlotPush");
        string completeMethod = ExtractMethod(source, "void CompleteSlotDrag");

        // Slots react to a real left-button press/move/release rather than the OLE drag loop.
        Assert.Contains("button.PreviewMouseLeftButtonDown += SlotButton_PreviewMouseLeftButtonDown", createButtonMethod);
        Assert.Contains("button.PreviewMouseMove += SlotButton_PreviewMouseMove", createButtonMethod);
        Assert.Contains("button.PreviewMouseLeftButtonUp += SlotButton_PreviewMouseLeftButtonUp", createButtonMethod);

        // Internal reordering must NOT use OLE DoDragDrop, which freezes the source-window animation mid-drag.
        Assert.DoesNotContain("DragDrop.DoDragDrop", source);
        Assert.Contains("BeginSlotDrag(button)", mouseMoveMethod);
        Assert.Contains("UpdateSlotDrag(currentPoint)", mouseMoveMethod);
        Assert.Contains("CaptureMouse()", beginDragMethod);
        Assert.Contains("UpdateSlotReorderPreview", updateDragMethod);

        // The displaced slots smoothly slide to their new cells (push-aside) on both axes.
        Assert.Contains("AnimateSlotPush(slotButton, offsetX, offsetY)", previewMethod);
        Assert.Contains("TranslateTransform.XProperty", pushMethod);
        Assert.Contains("TranslateTransform.YProperty", pushMethod);
        Assert.Contains("HandoffBehavior.SnapshotAndReplace", pushMethod);
        Assert.Contains("CubicEase", pushMethod);
        Assert.Contains("SlotReorderAnimationMilliseconds = 170", source);

        // Releasing the pointer commits the swap through the existing within-widget move.
        Assert.Contains("CompleteSlotDrag(commit: true)", mouseUpMethod);
        Assert.Contains("MoveSlotContentsWithinSelectedWidget", completeMethod);
    }

    private static string ReadMainWindowSource()
    {
        return File.ReadAllText(Path.Combine(FindSourceRoot(), "src", "PiusLauncher", "PiusLauncher", "MainWindow.cs"));
    }

    private static string ReadLauncherItemSource()
    {
        return File.ReadAllText(Path.Combine(FindSourceRoot(), "src", "PiusLauncher", "PiusLauncher", "LauncherItem.cs"));
    }

    private static string FindSourceRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "src", "PiusLauncher", "PiusLauncher", "MainWindow.cs");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find source root from test output directory.");
    }

    private static string ExtractMethod(string source, string methodName)
    {
        int methodNameIndex = source.IndexOf(methodName.Contains(' ') ? methodName : $"MenuItem {methodName}", StringComparison.Ordinal);
        if (methodNameIndex < 0)
        {
            throw new InvalidOperationException($"Could not find method {methodName}.");
        }

        int openBraceIndex = source.IndexOf('{', methodNameIndex);
        if (openBraceIndex < 0)
        {
            throw new InvalidOperationException($"Could not find body for method {methodName}.");
        }

        int depth = 0;
        for (int index = openBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(methodNameIndex, index - methodNameIndex + 1);
                }
            }
        }

        throw new InvalidOperationException($"Could not find end of method {methodName}.");
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}

public sealed class UiPreferencesStoreTests
{
    [Fact]
    public void SaveLanguage_and_LoadLanguage_roundtrip_english_in_a_separate_preferences_file()
    {
        string preferencesFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "ui-preferences.json");
        UiPreferencesStore store = new UiPreferencesStore(preferencesFile);

        store.SaveLanguage(UiLanguage.English);

        Assert.Equal(UiLanguage.English, new UiPreferencesStore(preferencesFile).LoadLanguage());
        Assert.Contains("English", File.ReadAllText(preferencesFile));
        Assert.DoesNotContain("LauncherTitle", File.ReadAllText(preferencesFile));
    }

    [Fact]
    public void LoadLanguage_defaults_to_korean_for_missing_malformed_or_unknown_preferences()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string missingFile = Path.Combine(tempDirectory, "missing.json");
        string malformedFile = Path.Combine(tempDirectory, "malformed.json");
        string unknownFile = Path.Combine(tempDirectory, "unknown.json");
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllText(malformedFile, "{ not json");
        File.WriteAllText(unknownFile, "{ \"Language\": \"Spanish\" }");

        Assert.Equal(UiLanguage.Korean, new UiPreferencesStore(missingFile).LoadLanguage());
        Assert.Equal(UiLanguage.Korean, new UiPreferencesStore(malformedFile).LoadLanguage());
        Assert.Equal(UiLanguage.Korean, new UiPreferencesStore(unknownFile).LoadLanguage());
    }
}

public sealed class LauncherIconAssetTests
{
    private const string OldIconSha256 = "E210DDAC4B69E793FCF0DA74F5EB5B30C2143054994C27F925D7933637C537F9";

    [Fact]
    public void App_icons_are_modern_luminous_multi_resolution_assets()
    {
        string sourceRoot = FindSourceRoot();
        string appIconPath = Path.Combine(sourceRoot, "src", "PiusLauncher", "app.ico");
        string resourceIconPath = Path.Combine(sourceRoot, "src", "PiusLauncher", "assets.appicon.ico");

        IconInspection appIcon = InspectIcon(appIconPath);
        IconInspection resourceIcon = InspectIcon(resourceIconPath);

        Assert.Equal(appIcon.Sha256, resourceIcon.Sha256);
        Assert.NotEqual(OldIconSha256, appIcon.Sha256);
        Assert.Contains(256, appIcon.Sizes);
        Assert.Contains(64, appIcon.Sizes);
        Assert.Contains(48, appIcon.Sizes);
        Assert.Contains(32, appIcon.Sizes);
        Assert.Contains(16, appIcon.Sizes);
        Assert.True(appIcon.WindowsLoadable, "Expected the icon to be loadable by the Windows icon loader.");
        Assert.True(appIcon.SaturatedPixelRatio > 0.28, $"Expected vivid color coverage, got {appIcon.SaturatedPixelRatio:0.000}.");
        Assert.True(appIcon.LuminousPixelRatio > 0.08, $"Expected luminous highlights, got {appIcon.LuminousPixelRatio:0.000}.");
        Assert.True(appIcon.HueBucketCount >= 5, $"Expected a colorful but refined palette, got {appIcon.HueBucketCount} hue buckets.");
    }

    private static IconInspection InspectIcon(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        IReadOnlyList<IconEntry> entries = ReadIconEntries(bytes);
        IconEntry largestEntry = entries.OrderByDescending(entry => entry.Width).First();
        IReadOnlyList<System.Drawing.Color> pixels = DecodeIconFrame(bytes, largestEntry);
        bool windowsLoadable = CanLoadWithWindowsIconLoader(path);
        int opaquePixels = 0;
        int saturatedPixels = 0;
        int luminousPixels = 0;
        HashSet<int> hueBuckets = new HashSet<int>();
        foreach (System.Drawing.Color pixel in pixels)
        {
            if (pixel.A < 48)
            {
                continue;
            }

            opaquePixels++;
            float saturation = pixel.GetSaturation();
            float brightness = pixel.GetBrightness();
            if (saturation > 0.52f && brightness > 0.34f)
            {
                saturatedPixels++;
                hueBuckets.Add((int)(pixel.GetHue() / 24.0f));
            }

            if (brightness > 0.82f && saturation > 0.16f)
            {
                luminousPixels++;
            }
        }

        return new IconInspection(
            hash,
            entries.Select(entry => entry.Width).OrderBy(size => size).Distinct().ToArray(),
            windowsLoadable,
            opaquePixels == 0 ? 0.0 : (double)saturatedPixels / opaquePixels,
            opaquePixels == 0 ? 0.0 : (double)luminousPixels / opaquePixels,
            hueBuckets.Count);
    }

    private static IReadOnlyList<System.Drawing.Color> DecodeIconFrame(byte[] iconBytes, IconEntry entry)
    {
        if (iconBytes[entry.Offset] == 0x89 && iconBytes[entry.Offset + 1] == 0x50)
        {
            using MemoryStream stream = new MemoryStream(iconBytes, entry.Offset, entry.BytesInResource, writable: false);
            using System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(stream);
            List<System.Drawing.Color> colors = new List<System.Drawing.Color>(bitmap.Width * bitmap.Height);
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    colors.Add(bitmap.GetPixel(x, y));
                }
            }

            return colors;
        }

        int headerSize = BitConverter.ToInt32(iconBytes, entry.Offset);
        int width = BitConverter.ToInt32(iconBytes, entry.Offset + 4);
        int height = BitConverter.ToInt32(iconBytes, entry.Offset + 8) / 2;
        int bitCount = BitConverter.ToUInt16(iconBytes, entry.Offset + 14);
        if (headerSize != 40 || width <= 0 || height <= 0 || bitCount != 32)
        {
            throw new InvalidDataException("The largest icon frame is not a supported 32-bit DIB or PNG frame.");
        }

        int pixelOffset = entry.Offset + headerSize;
        List<System.Drawing.Color> pixels = new List<System.Drawing.Color>(width * height);
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelIndex = pixelOffset + (((y * width) + x) * 4);
                pixels.Add(System.Drawing.Color.FromArgb(
                    iconBytes[pixelIndex + 3],
                    iconBytes[pixelIndex + 2],
                    iconBytes[pixelIndex + 1],
                    iconBytes[pixelIndex]));
            }
        }

        return pixels;
    }

    private static bool CanLoadWithWindowsIconLoader(string path)
    {
        try
        {
            using System.Drawing.Icon mediumIcon = new System.Drawing.Icon(path, 64, 64);
            using System.Drawing.Bitmap mediumBitmap = mediumIcon.ToBitmap();
            using System.Drawing.Icon largeIcon = new System.Drawing.Icon(path, 256, 256);
            using System.Drawing.Bitmap largeBitmap = largeIcon.ToBitmap();
            return mediumIcon.Width > 0 && mediumBitmap.Width > 0 && largeIcon.Width > 0 && largeBitmap.Width > 0;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<IconEntry> ReadIconEntries(byte[] bytes)
    {
        if (bytes.Length < 6 || BitConverter.ToUInt16(bytes, 0) != 0 || BitConverter.ToUInt16(bytes, 2) != 1)
        {
            throw new InvalidDataException("The icon file is not a Windows ICO.");
        }

        int count = BitConverter.ToUInt16(bytes, 4);
        List<IconEntry> entries = new List<IconEntry>();
        for (int index = 0; index < count; index++)
        {
            int offset = 6 + (index * 16);
            int width = bytes[offset] == 0 ? 256 : bytes[offset];
            int bytesInResource = BitConverter.ToInt32(bytes, offset + 8);
            int imageOffset = BitConverter.ToInt32(bytes, offset + 12);
            if (imageOffset < 0 || bytesInResource <= 0 || imageOffset + bytesInResource > bytes.Length)
            {
                throw new InvalidDataException("The icon file has an invalid image directory entry.");
            }

            entries.Add(new IconEntry(width, bytesInResource, imageOffset));
        }

        return entries;
    }

    private static string FindSourceRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "src", "PiusLauncher", "app.ico");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find source root from test output directory.");
    }

    private sealed record IconEntry(int Width, int BytesInResource, int Offset);

    private sealed record IconInspection(string Sha256, IReadOnlyList<int> Sizes, bool WindowsLoadable, double SaturatedPixelRatio, double LuminousPixelRatio, int HueBucketCount);
}

public sealed class LauncherStoreTests
{
    [Fact]
    public void LoadInto_uses_p_edu_launcher_as_the_default_title()
    {
        // Given: a fresh store with no saved launcher title.
        string storageFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "launcher-items.json");
        LauncherStore store = new LauncherStore(storageFile);

        // When: the app loads its display settings.
        LauncherDisplaySettings settings = store.LoadInto(new[] { CreateWidget(), CreateWidget(), CreateWidget() });

        // Then: the default app title matches the executable/folder name.
        Assert.Equal("P-Edu-Launcher", settings.LauncherTitle);
    }

    [Fact]
    public void LoadInto_migrates_the_legacy_default_titles_to_p_edu_launcher()
    {
        // Given: existing settings files that still contain old default titles.
        string storageFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "launcher-items.json");
        Directory.CreateDirectory(Path.GetDirectoryName(storageFile)!);
        File.WriteAllText(storageFile, """
            {
              "LauncherTitle": "PIUS 런처",
              "WidgetNames": ["One", "Two", "Three"],
              "SelectedWidgetIndex": 0,
              "Items": []
            }
            """);
        string englishStorageFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "launcher-items.json");
        Directory.CreateDirectory(Path.GetDirectoryName(englishStorageFile)!);
        File.WriteAllText(englishStorageFile, """
            {
              "LauncherTitle": "PIUS Launcher",
              "WidgetNames": ["One", "Two", "Three"],
              "SelectedWidgetIndex": 0,
              "Items": []
            }
            """);

        // When: those legacy files are loaded.
        LauncherDisplaySettings koreanSettings = new LauncherStore(storageFile).LoadInto(new[] { CreateWidget(), CreateWidget(), CreateWidget() });
        LauncherDisplaySettings englishSettings = new LauncherStore(englishStorageFile).LoadInto(new[] { CreateWidget(), CreateWidget(), CreateWidget() });

        // Then: both old defaults are treated as the new default instead of staying stale.
        Assert.Equal("P-Edu-Launcher", koreanSettings.LauncherTitle);
        Assert.Equal("P-Edu-Launcher", englishSettings.LauncherTitle);
    }

    [Fact]
    public void Save_and_load_roundtrips_multiple_targets_per_slot()
    {
        // Given: a store with an isolated file and one slot containing a folder, app, file, and site shortcut.
        string storageFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "launcher-items.json");
        LauncherStore store = new LauncherStore(storageFile);
        Collection<LauncherItem> widget = CreateWidget();
        widget[0].AddTarget(@"C:\Learning", "Learning");
        widget[0].AddTarget(@"C:\Tools\Paint.exe", "Paint");
        widget[0].AddTarget(@"C:\Notes\plan.txt", "Plan");
        widget[0].AddTarget(@"https://example.com", "Site");

        // When: the state is saved and loaded into a fresh model.
        store.Save(widget, "PIUS 런처", new[] { new WidgetTab(0), new WidgetTab(1), new WidgetTab(2) }, selectedWidgetIndex: 0);
        Collection<LauncherItem> loadedWidget = CreateWidget();
        store.LoadInto(new[] { loadedWidget, CreateWidget(), CreateWidget() });

        // Then: all objects in the slot survive the round trip in order.
        Assert.Equal(4, loadedWidget[0].TargetCount);
        Assert.Equal(new[] { @"C:\Learning", @"C:\Tools\Paint.exe", @"C:\Notes\plan.txt", @"https://example.com" }, loadedWidget[0].Targets.Select(target => target.TargetPath));
        Assert.Equal("Learning +3", loadedWidget[0].DisplayName);
    }

    [Fact]
    public void Save_and_load_roundtrips_ten_targets_per_slot()
    {
        // Given: a store with an isolated file and one slot containing ten shortcuts.
        string storageFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "launcher-items.json");
        LauncherStore store = new LauncherStore(storageFile);
        Collection<LauncherItem> widget = CreateWidget();
        for (int index = 1; index <= 10; index++)
        {
            widget[0].AddTarget($@"C:\Learning\App{index}.exe", $"App {index}");
        }
        widget[0].AddTarget(@"C:\Learning\App11.exe", "App 11");

        // When: the state is saved and loaded into a fresh model.
        store.Save(widget, "P-Edu-Launcher", new[] { new WidgetTab(0), new WidgetTab(1), new WidgetTab(2) }, selectedWidgetIndex: 0);
        Collection<LauncherItem> loadedWidget = CreateWidget();
        store.LoadInto(new[] { loadedWidget, CreateWidget(), CreateWidget() });

        // Then: ten objects survive the round trip and the overflow object stays absent.
        Assert.Equal(10, loadedWidget[0].TargetCount);
        Assert.Equal("App 1 +9", loadedWidget[0].DisplayName);
        Assert.Equal("10개 일괄 실행", loadedWidget[0].Subtitle);
        Assert.Equal(@"C:\Learning\App10.exe", loadedWidget[0].Targets[9].TargetPath);
        Assert.DoesNotContain(loadedWidget[0].Targets, target => string.Equals(target.TargetPath, @"C:\Learning\App11.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Save_and_load_preserves_slot_contents_after_drag_reorder()
    {
        string storageFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "launcher-items.json");
        LauncherStore store = new LauncherStore(storageFile);
        Collection<LauncherItem> widget = CreateWidget();
        widget[0].AddTarget(@"C:\Study\Alpha.exe", "Alpha");
        widget[0].SetSlotName("Alpha Slot");
        widget[1].AddTarget(@"C:\Study\Beta.exe", "Beta");
        widget[2].AddTarget("https://course.example", "Course");

        LauncherItem.MoveContentsWithinWidget(widget, sourceSlotIndex: 0, targetSlotIndex: 2);
        store.Save(widget, "P-Edu-Launcher", new[] { new WidgetTab(0), new WidgetTab(1), new WidgetTab(2) }, selectedWidgetIndex: 0);
        Collection<LauncherItem> loadedWidget = CreateWidget();
        store.LoadInto(new[] { loadedWidget, CreateWidget(), CreateWidget() });

        Assert.Equal(new[] { @"C:\Study\Beta.exe", "https://course.example", @"C:\Study\Alpha.exe" }, loadedWidget.Take(3).Select(item => item.TargetPath));
        Assert.Equal("Alpha Slot", loadedWidget[2].SlotName);
    }

    [Fact]
    public void SaveCurrentSettingsSnapshot_and_load_roundtrip_through_saved_settings_folder()
    {
        // Given: a current store and a separate top-level app folder for saved settings.
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string storageFile = Path.Combine(tempRoot, "current", "launcher-items.json");
        string appTopLevelFolder = Path.Combine(tempRoot, "P-Edu-Launcher");
        Directory.CreateDirectory(appTopLevelFolder);
        LauncherStore store = new LauncherStore(storageFile);
        Collection<LauncherItem> widget1 = CreateWidget(widgetIndex: 0);
        Collection<LauncherItem> widget2 = CreateWidget(widgetIndex: 1);
        Collection<LauncherItem> widget3 = CreateWidget(widgetIndex: 2);
        widget3[2].AddTarget(@"C:\Study\Math.exe", "Math");
        widget3[2].AddTarget("https://example.edu", "Course");
        widget3[2].SetSlotName("Widget Three Study");
        WidgetTab[] tabs = { new WidgetTab(0), new WidgetTab(1), new WidgetTab(2) };
        tabs[2].Name = "위젯3";

        // When: the user saves a snapshot and loads it back into a fresh launcher model.
        string snapshotFile = store.SaveCurrentSettingsSnapshot(appTopLevelFolder, new[] { widget1, widget2, widget3 }.SelectMany(widget => widget), "P-Edu-Launcher", tabs, selectedWidgetIndex: 2);
        Collection<LauncherItem> loadedWidget3 = CreateWidget(widgetIndex: 2);
        LauncherDisplaySettings loadedSettings = store.LoadSavedSettingsSnapshot(appTopLevelFolder, new[] { CreateWidget(widgetIndex: 0), CreateWidget(widgetIndex: 1), loadedWidget3 });

        // Then: the snapshot lives under the app top-level [저장된 설정] folder and fully restores the current state.
        Assert.Equal(Path.Combine(appTopLevelFolder, "저장된 설정", "launcher-items.json"), snapshotFile);
        Assert.True(File.Exists(snapshotFile));
        Assert.Equal("위젯3", loadedSettings.WidgetNames[2]);
        Assert.Equal(2, loadedSettings.SelectedWidgetIndex);
        Assert.Equal("Widget Three Study", loadedWidget3[2].SlotName);
        Assert.Equal(new[] { @"C:\Study\Math.exe", "https://example.edu" }, loadedWidget3[2].Targets.Select(target => target.TargetPath));
    }

    [Fact]
    public void SaveCurrentSettingsSnapshotFile_writes_to_the_user_chosen_json_file()
    {
        // Given: a current launcher state and a JSON path chosen from the Save dialog.
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string storageFile = Path.Combine(tempRoot, "current", "launcher-items.json");
        string chosenSnapshotFile = Path.Combine(tempRoot, "custom folder", "renamed-study-settings.json");
        LauncherStore store = new LauncherStore(storageFile);
        Collection<LauncherItem> widget = CreateWidget(widgetIndex: 1);
        widget[4].AddTarget(@"C:\Study\Physics.exe", "Physics");
        WidgetTab[] tabs = { new WidgetTab(0), new WidgetTab(1), new WidgetTab(2) };
        tabs[1].Name = "과학";

        // When: the settings dialog asks the store to save to that chosen file.
        string savedFile = store.SaveCurrentSettingsSnapshotFile(chosenSnapshotFile, widget, "P-Edu-Launcher", tabs, selectedWidgetIndex: 1);

        // Then: the snapshot is written exactly where the user chose, not to the fixed default filename.
        Assert.Equal(chosenSnapshotFile, savedFile);
        Assert.True(File.Exists(chosenSnapshotFile));
        string snapshotJson = File.ReadAllText(chosenSnapshotFile);
        Assert.Contains("renamed-study-settings.json", chosenSnapshotFile);
        Assert.Contains("Physics", snapshotJson);
    }

    [Fact]
    public void LoadSavedSettingsSnapshotFile_loads_the_user_chosen_json_file()
    {
        // Given: a user-chosen saved settings JSON and a different live store.
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string storageFile = Path.Combine(tempRoot, "current", "launcher-items.json");
        string chosenSnapshotFile = Path.Combine(tempRoot, "saved", "math-session.json");
        LauncherStore store = new LauncherStore(storageFile);
        Collection<LauncherItem> savedWidget = CreateWidget(widgetIndex: 2);
        savedWidget[2].AddTarget(@"C:\Study\Math.exe", "Math");
        savedWidget[2].SetSlotName("Math Session");
        WidgetTab[] tabs = { new WidgetTab(0), new WidgetTab(1), new WidgetTab(2) };
        tabs[2].Name = "수학";
        store.SaveCurrentSettingsSnapshotFile(chosenSnapshotFile, savedWidget, "P-Edu-Launcher", tabs, selectedWidgetIndex: 2);
        File.WriteAllText(storageFile, """
            {
              "LauncherTitle": "Different",
              "WidgetNames": ["A", "B", "C"],
              "SelectedWidgetIndex": 0,
              "Items": []
            }
            """);
        Collection<LauncherItem> loadedWidget = CreateWidget(widgetIndex: 2);

        // When: the settings dialog loads that chosen file.
        LauncherDisplaySettings loadedSettings = store.LoadSavedSettingsSnapshotFile(chosenSnapshotFile, new[] { CreateWidget(widgetIndex: 0), CreateWidget(widgetIndex: 1), loadedWidget });

        // Then: the chosen JSON is copied into the live store and applied to the visible launcher model.
        Assert.Equal("수학", loadedSettings.WidgetNames[2]);
        Assert.Equal(2, loadedSettings.SelectedWidgetIndex);
        Assert.Equal("Math Session", loadedWidget[2].SlotName);
        Assert.Equal(@"C:\Study\Math.exe", loadedWidget[2].TargetPath);
        Assert.Contains("Math Session", File.ReadAllText(storageFile));
    }

    [Fact]
    public void LoadSavedSettingsSnapshotFile_rejects_malformed_json_without_overwriting_live_store()
    {
        // Given: a live store and visible launcher model that should survive a bad selected file.
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string storageFile = Path.Combine(tempRoot, "current", "launcher-items.json");
        string malformedSnapshotFile = Path.Combine(tempRoot, "saved", "broken.json");
        LauncherStore store = new LauncherStore(storageFile);
        Collection<LauncherItem> liveWidget = CreateWidget();
        liveWidget[0].AddTarget(@"C:\Study\Current.exe", "Current");
        store.Save(liveWidget, "P-Edu-Launcher", new[] { new WidgetTab(0), new WidgetTab(1), new WidgetTab(2) }, selectedWidgetIndex: 0);
        string liveStoreBeforeLoad = File.ReadAllText(storageFile);
        Directory.CreateDirectory(Path.GetDirectoryName(malformedSnapshotFile)!);
        File.WriteAllText(malformedSnapshotFile, "{ this is not json");

        // When: the user chooses the malformed JSON in the Open dialog.
        Assert.Throws<JsonException>(() => store.LoadSavedSettingsSnapshotFile(malformedSnapshotFile, new[] { liveWidget, CreateWidget(widgetIndex: 1), CreateWidget(widgetIndex: 2) }));

        // Then: the live store and visible launcher item remain unchanged.
        Assert.Equal(liveStoreBeforeLoad, File.ReadAllText(storageFile));
        Assert.Equal(@"C:\Study\Current.exe", liveWidget[0].TargetPath);
        Assert.Equal("Current", liveWidget[0].DisplayName);
    }

    [Fact]
    public void LoadInto_migrates_legacy_single_target_records()
    {
        // Given: the existing deployed JSON shape with one TargetPath per slot.
        string storageFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "launcher-items.json");
        Directory.CreateDirectory(Path.GetDirectoryName(storageFile)!);
        File.WriteAllText(storageFile, """
            {
              "LauncherTitle": "Legacy",
              "WidgetNames": ["One", "Two", "Three"],
              "SelectedWidgetIndex": 0,
              "Items": [
                {
                  "WidgetIndex": 0,
                  "SlotIndex": 1,
                  "TargetPath": "C:\\Legacy\\Only.exe",
                  "DisplayName": "Only",
                  "Kind": 1
                }
              ]
            }
            """);
        LauncherStore store = new LauncherStore(storageFile);
        Collection<LauncherItem> widget = CreateWidget();

        // When: that legacy file is loaded.
        LauncherDisplaySettings settings = store.LoadInto(new[] { widget, CreateWidget(), CreateWidget() });

        // Then: the old record becomes the first object in the slot.
        Assert.Equal("Legacy", settings.LauncherTitle);
        Assert.Single(widget[1].Targets);
        Assert.Equal(@"C:\Legacy\Only.exe", widget[1].TargetPath);
        Assert.Equal("Only", widget[1].DisplayName);
    }

    [Fact]
    public void Save_and_load_roundtrips_after_removing_one_group_target()
    {
        // Given: a grouped slot that has one object removed before persistence.
        string storageFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "launcher-items.json");
        LauncherStore store = new LauncherStore(storageFile);
        Collection<LauncherItem> widget = CreateWidget();
        widget[0].AddTarget(@"C:\Learning", "Learning");
        widget[0].AddTarget(@"C:\Tools\Paint.exe", "Paint");
        widget[0].AddTarget(@"https://example.com", "Site");
        widget[0].RemoveTargetAt(1);

        // When: the changed slot is saved and reloaded.
        store.Save(widget, "PIUS 런처", new[] { new WidgetTab(0), new WidgetTab(1), new WidgetTab(2) }, selectedWidgetIndex: 0);
        Collection<LauncherItem> loadedWidget = CreateWidget();
        store.LoadInto(new[] { loadedWidget, CreateWidget(), CreateWidget() });

        // Then: the deleted object remains absent and the remaining objects keep their order.
        Assert.Equal(2, loadedWidget[0].TargetCount);
        Assert.Equal(new[] { @"C:\Learning", "https://example.com" }, loadedWidget[0].Targets.Select(target => target.TargetPath));
        Assert.Equal("Learning +1", loadedWidget[0].DisplayName);
    }

    [Fact]
    public void Save_and_load_roundtrips_slot_name_separately_from_targets()
    {
        // Given: a grouped slot with a custom slot name and target-level display names.
        string storageFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "launcher-items.json");
        LauncherStore store = new LauncherStore(storageFile);
        Collection<LauncherItem> widget = CreateWidget();
        widget[0].AddTarget(@"C:\Learning", "Learning");
        widget[0].AddTarget(@"https://example.com", "Site");
        widget[0].SetSlotName("Morning Study");

        // When: the slot is saved and loaded into a fresh model.
        store.Save(widget, "PIUS 런처", new[] { new WidgetTab(0), new WidgetTab(1), new WidgetTab(2) }, selectedWidgetIndex: 0);
        Collection<LauncherItem> loadedWidget = CreateWidget();
        store.LoadInto(new[] { loadedWidget, CreateWidget(), CreateWidget() });

        // Then: the slot name and the target names are both preserved.
        Assert.Equal("Morning Study", loadedWidget[0].SlotName);
        Assert.Equal("Morning Study", loadedWidget[0].DisplayName);
        Assert.Equal(new[] { "Learning", "Site" }, loadedWidget[0].Targets.Select(target => target.DisplayName));
    }

    private static Collection<LauncherItem> CreateWidget(int widgetIndex = 0)
    {
        Collection<LauncherItem> widget = new Collection<LauncherItem>();
        for (int slotIndex = 0; slotIndex < 10; slotIndex++)
        {
            widget.Add(new LauncherItem(widgetIndex, slotIndex));
        }

        return widget;
    }
}
