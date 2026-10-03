// src/DiscordBot.Bot/Pages/Components.cshtml.cs
using DiscordBot.Bot.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Bot.ViewModels.Components;

namespace DiscordBot.Bot.Pages;

/// <summary>
/// PageModel for the component showcase page.
/// Creates sample ViewModels for all UI components to demonstrate their various states and variants.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class ComponentsModel : PageModel
{
    // Buttons
    public List<ButtonViewModel> ButtonVariants { get; set; } = new();
    public List<ButtonViewModel> ButtonSizes { get; set; } = new();
    public List<ButtonViewModel> IconButtons { get; set; } = new();
    public ButtonViewModel LoadingButton { get; set; } = new();

    // Badges
    public List<BadgeViewModel> BadgeFilledVariants { get; set; } = new();
    public List<BadgeViewModel> BadgeOutlineVariants { get; set; } = new();
    public List<BadgeViewModel> BadgeSizes { get; set; } = new();

    // Status Indicators
    public List<StatusIndicatorViewModel> StatusDotOnly { get; set; } = new();
    public List<StatusIndicatorViewModel> StatusWithText { get; set; } = new();
    public List<StatusIndicatorViewModel> StatusBadgeStyle { get; set; } = new();
    public List<StatusIndicatorViewModel> StatusPulsing { get; set; } = new();

    // Loading Spinners
    public List<LoadingSpinnerViewModel> SpinnerVariants { get; set; } = new();
    public List<LoadingSpinnerViewModel> SpinnerSizes { get; set; } = new();
    public LoadingSpinnerViewModel SpinnerWithMessage { get; set; } = new();

    // Form Inputs
    public FormInputViewModel BasicInput { get; set; } = new();
    public List<FormInputViewModel> InputSizes { get; set; } = new();
    public FormInputViewModel InputWithIcon { get; set; } = new();
    public List<FormInputViewModel> InputValidationStates { get; set; } = new();

    // Form Selects
    public FormSelectViewModel BasicSelect { get; set; } = new();
    public FormSelectViewModel SelectWithGroups { get; set; } = new();
    public List<FormSelectViewModel> SelectValidationStates { get; set; } = new();

    // Alerts
    public List<AlertViewModel> AlertVariants { get; set; } = new();
    public List<AlertViewModel> AlertDismissible { get; set; } = new();

    // Confirmation Dialogs
    public ConfirmationModalViewModel InfoConfirmation { get; set; } = new();
    public ConfirmationModalViewModel WarningConfirmation { get; set; } = new();
    public ConfirmationModalViewModel DangerConfirmation { get; set; } = new();
    public TypedConfirmationModalViewModel TypedConfirmation { get; set; } = new();

    // Cards
    public CardViewModel DefaultCard { get; set; } = new();
    public CardViewModel ElevatedCard { get; set; } = new();
    public CardViewModel InteractiveCard { get; set; } = new();
    public CardViewModel CollapsibleCard { get; set; } = new();

    // Empty States
    public List<EmptyStateViewModel> EmptyStateTypes { get; set; } = new();

    // Pagination
    public List<PaginationViewModel> PaginationStyles { get; set; } = new();

    // Interaction primitives (UX plan Phase 3)
    public List<FormInputViewModel> InputAttributeExamples { get; set; } = new();
    public List<FormTextareaViewModel> TextareaStates { get; set; } = new();
    public RadioCardGroupViewModel RadioCardsDefault { get; set; } = new();
    public RadioCardGroupViewModel RadioCardsIcons { get; set; } = new();
    public RadioCardGroupViewModel RadioCardsInvalid { get; set; } = new();
    public List<FormToggleViewModel> ToggleStates { get; set; } = new();
    public List<FormToggleViewModel> ToggleFormDemo { get; set; } = new();
    public ConfirmationModalViewModel RedirectConfirmation { get; set; } = new();
    public ConfirmationModalViewModel FailingConfirmation { get; set; } = new();
    public List<(string Title, string Note, PaginationViewModel Model)> PaginationStates { get; set; } = new();
    public List<(string Title, EmptyStateViewModel Model)> EmptyStateVariants { get; set; } = new();

    // NavTabs
    public NavTabsViewModel UnderlineTabs { get; set; } = default!;
    public NavTabsViewModel PillsTabs { get; set; } = default!;
    public NavTabsViewModel BorderedTabs { get; set; } = default!;
    public NavTabsViewModel IconTabs { get; set; } = default!;

    public IActionResult OnPostShowcaseConfirm()
    {
        return new JsonResult(new { success = true, message = "Showcase confirmation received." });
    }

    /// <summary>
    /// A handler that answers with a redirect and a TempData toast, like Users/Edit Reset Password
    /// and LinkDiscord Unlink. The confirmation modal must not follow the redirect (that would spend
    /// the toast); the page loads once and shows it.
    /// </summary>
    public IActionResult OnPostShowcaseRedirect()
    {
        TempData.SetSuccessToast("The redirecting handler finished. This toast came through TempData.");
        return RedirectToPage();
    }

    /// <summary>A handler that fails, to show the modal's error state (it stays open, the button re-enables).</summary>
    public IActionResult OnPostShowcaseFail()
    {
        return new JsonResult(new { success = false, message = "Showcase failure: this is how a failed action reads." })
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    public void OnGet()
    {
        InitializeButtons();
        InitializeBadges();
        InitializeStatusIndicators();
        InitializeLoadingSpinners();
        InitializeFormInputs();
        InitializeFormSelects();
        InitializeAlerts();
        InitializeConfirmationDialogs();
        InitializeCards();
        InitializeEmptyStates();
        InitializePagination();
        InitializeInteractionPrimitives();
        InitializeNavTabs();
    }

    private void InitializeButtons()
    {
        // Button Variants
        ButtonVariants = new List<ButtonViewModel>
        {
            new() { Text = "Primary Button", Variant = ButtonVariant.Primary },
            new() { Text = "Secondary Button", Variant = ButtonVariant.Secondary },
            new() { Text = "Accent Button", Variant = ButtonVariant.Accent },
            new() { Text = "Danger Button", Variant = ButtonVariant.Danger },
            new() { Text = "Ghost Button", Variant = ButtonVariant.Ghost }
        };

        // Button Sizes
        ButtonSizes = new List<ButtonViewModel>
        {
            new() { Text = "Small Button", Size = ButtonSize.Small },
            new() { Text = "Medium Button", Size = ButtonSize.Medium },
            new() { Text = "Large Button", Size = ButtonSize.Large }
        };

        // Icon Buttons
        IconButtons = new List<ButtonViewModel>
        {
            new() { Text = "Save", Variant = ButtonVariant.Primary, IconLeft = "M5 13l4 4L19 7" },
            new() { Text = "Delete", Variant = ButtonVariant.Danger, IconLeft = "M6 18L18 6M6 6l12 12" },
            new() { IsIconOnly = true, Variant = ButtonVariant.Ghost, IconLeft = "M15 12a3 3 0 11-6 0 3 3 0 016 0z", AriaLabel = "Settings" }
        };

        // Loading Button
        LoadingButton = new ButtonViewModel
        {
            Text = "Loading...",
            Variant = ButtonVariant.Primary,
            IsLoading = true,
            IsDisabled = true
        };
    }

    private void InitializeBadges()
    {
        // Filled Badges
        BadgeFilledVariants = new List<BadgeViewModel>
        {
            new() { Text = "Default", Variant = BadgeVariant.Default, Style = BadgeStyle.Filled },
            new() { Text = "Orange", Variant = BadgeVariant.Orange, Style = BadgeStyle.Filled },
            new() { Text = "Blue", Variant = BadgeVariant.Blue, Style = BadgeStyle.Filled },
            new() { Text = "Success", Variant = BadgeVariant.Success, Style = BadgeStyle.Filled },
            new() { Text = "Warning", Variant = BadgeVariant.Warning, Style = BadgeStyle.Filled },
            new() { Text = "Error", Variant = BadgeVariant.Error, Style = BadgeStyle.Filled },
            new() { Text = "Info", Variant = BadgeVariant.Info, Style = BadgeStyle.Filled }
        };

        // Outline Badges
        BadgeOutlineVariants = new List<BadgeViewModel>
        {
            new() { Text = "Default", Variant = BadgeVariant.Default, Style = BadgeStyle.Outline },
            new() { Text = "Orange", Variant = BadgeVariant.Orange, Style = BadgeStyle.Outline },
            new() { Text = "Blue", Variant = BadgeVariant.Blue, Style = BadgeStyle.Outline },
            new() { Text = "Success", Variant = BadgeVariant.Success, Style = BadgeStyle.Outline },
            new() { Text = "Warning", Variant = BadgeVariant.Warning, Style = BadgeStyle.Outline },
            new() { Text = "Error", Variant = BadgeVariant.Error, Style = BadgeStyle.Outline },
            new() { Text = "Info", Variant = BadgeVariant.Info, Style = BadgeStyle.Outline }
        };

        // Badge Sizes
        BadgeSizes = new List<BadgeViewModel>
        {
            new() { Text = "Small", Size = BadgeSize.Small, Variant = BadgeVariant.Orange },
            new() { Text = "Medium", Size = BadgeSize.Medium, Variant = BadgeVariant.Orange },
            new() { Text = "Large", Size = BadgeSize.Large, Variant = BadgeVariant.Orange }
        };
    }

    private void InitializeStatusIndicators()
    {
        // Dot Only
        StatusDotOnly = new List<StatusIndicatorViewModel>
        {
            new() { Status = StatusType.Online, DisplayStyle = StatusDisplayStyle.DotOnly },
            new() { Status = StatusType.Idle, DisplayStyle = StatusDisplayStyle.DotOnly },
            new() { Status = StatusType.Busy, DisplayStyle = StatusDisplayStyle.DotOnly },
            new() { Status = StatusType.Offline, DisplayStyle = StatusDisplayStyle.DotOnly }
        };

        // Dot With Text
        StatusWithText = new List<StatusIndicatorViewModel>
        {
            new() { Status = StatusType.Online, Text = "Online", DisplayStyle = StatusDisplayStyle.DotWithText },
            new() { Status = StatusType.Idle, Text = "Idle", DisplayStyle = StatusDisplayStyle.DotWithText },
            new() { Status = StatusType.Busy, Text = "Busy", DisplayStyle = StatusDisplayStyle.DotWithText },
            new() { Status = StatusType.Offline, Text = "Offline", DisplayStyle = StatusDisplayStyle.DotWithText }
        };

        // Badge Style
        StatusBadgeStyle = new List<StatusIndicatorViewModel>
        {
            new() { Status = StatusType.Online, Text = "Online", DisplayStyle = StatusDisplayStyle.BadgeStyle },
            new() { Status = StatusType.Idle, Text = "Away", DisplayStyle = StatusDisplayStyle.BadgeStyle },
            new() { Status = StatusType.Busy, Text = "Do Not Disturb", DisplayStyle = StatusDisplayStyle.BadgeStyle },
            new() { Status = StatusType.Offline, Text = "Offline", DisplayStyle = StatusDisplayStyle.BadgeStyle }
        };

        // Pulsing
        StatusPulsing = new List<StatusIndicatorViewModel>
        {
            new() { Status = StatusType.Online, Text = "Bot Active", DisplayStyle = StatusDisplayStyle.DotWithText, IsPulsing = true }
        };
    }

    private void InitializeLoadingSpinners()
    {
        // Spinner Variants
        SpinnerVariants = new List<LoadingSpinnerViewModel>
        {
            new() { Variant = SpinnerVariant.Simple, Color = SpinnerColor.Blue },
            new() { Variant = SpinnerVariant.Dots, Color = SpinnerColor.Orange },
            new() { Variant = SpinnerVariant.Pulse, Color = SpinnerColor.Blue }
        };

        // Spinner Sizes
        SpinnerSizes = new List<LoadingSpinnerViewModel>
        {
            new() { Variant = SpinnerVariant.Simple, Size = SpinnerSize.Small },
            new() { Variant = SpinnerVariant.Simple, Size = SpinnerSize.Medium },
            new() { Variant = SpinnerVariant.Simple, Size = SpinnerSize.Large }
        };

        // Spinner With Message
        SpinnerWithMessage = new LoadingSpinnerViewModel
        {
            Variant = SpinnerVariant.Pulse,
            Size = SpinnerSize.Medium,
            Message = "Loading data...",
            SubMessage = "Please wait while we fetch your information"
        };
    }

    private void InitializeFormInputs()
    {
        // Basic Input
        BasicInput = new FormInputViewModel
        {
            Id = "username",
            Name = "username",
            Label = "Username",
            Placeholder = "Enter your username",
            HelpText = "Choose a unique username for your account"
        };

        // Input Sizes
        InputSizes = new List<FormInputViewModel>
        {
            new() { Id = "input-small", Name = "input-small", Label = "Small Input", Size = InputSize.Small, Placeholder = "Small size" },
            new() { Id = "input-medium", Name = "input-medium", Label = "Medium Input", Size = InputSize.Medium, Placeholder = "Medium size" },
            new() { Id = "input-large", Name = "input-large", Label = "Large Input", Size = InputSize.Large, Placeholder = "Large size" }
        };

        // Input With Icon
        InputWithIcon = new FormInputViewModel
        {
            Id = "search",
            Name = "search",
            Type = "search",
            Placeholder = "Search...",
            IconLeft = "M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
        };

        // Input Validation States
        InputValidationStates = new List<FormInputViewModel>
        {
            new() { Id = "valid", Name = "valid", Label = "Valid Input", Value = "valid@example.com", ValidationState = ValidationState.Success, ValidationMessage = "Email is available" },
            new() { Id = "warning", Name = "warning", Label = "Warning Input", Value = "test", ValidationState = ValidationState.Warning, ValidationMessage = "This username is similar to others" },
            new() { Id = "error", Name = "error", Label = "Error Input", Value = "invalid", ValidationState = ValidationState.Error, ValidationMessage = "This field is required" }
        };
    }

    private void InitializeFormSelects()
    {
        // Basic Select
        BasicSelect = new FormSelectViewModel
        {
            Id = "role",
            Name = "role",
            Label = "User Role",
            Options = new List<SelectOption>
            {
                new() { Value = "", Text = "Select a role" },
                new() { Value = "admin", Text = "Administrator" },
                new() { Value = "moderator", Text = "Moderator" },
                new() { Value = "member", Text = "Member" }
            },
            HelpText = "Choose the user's role in the server"
        };

        // Select With Groups
        SelectWithGroups = new FormSelectViewModel
        {
            Id = "server",
            Name = "server",
            Label = "Discord Server",
            OptionGroups = new List<SelectOptionGroup>
            {
                new()
                {
                    Label = "Gaming Servers",
                    Options = new List<SelectOption>
                    {
                        new() { Value = "game1", Text = "Awesome Gaming Community" },
                        new() { Value = "game2", Text = "Epic Gamers Unite" }
                    }
                },
                new()
                {
                    Label = "Developer Servers",
                    Options = new List<SelectOption>
                    {
                        new() { Value = "dev1", Text = "Code Collective" },
                        new() { Value = "dev2", Text = "Dev Community Hub" }
                    }
                }
            }
        };

        // Select Validation States
        SelectValidationStates = new List<FormSelectViewModel>
        {
            new() { Id = "select-success", Name = "select-success", Label = "Valid Selection", SelectedValue = "option1", ValidationState = ValidationState.Success, ValidationMessage = "Selection confirmed", Options = new List<SelectOption> { new() { Value = "option1", Text = "Option 1" } } },
            new() { Id = "select-error", Name = "select-error", Label = "Invalid Selection", ValidationState = ValidationState.Error, ValidationMessage = "Please select an option", Options = new List<SelectOption> { new() { Value = "", Text = "Choose..." } } }
        };
    }

    private void InitializeAlerts()
    {
        // Alert Variants
        AlertVariants = new List<AlertViewModel>
        {
            new() { Variant = AlertVariant.Info, Title = "Information", Message = "This is an informational alert. Use it to provide helpful tips or neutral information." },
            new() { Variant = AlertVariant.Success, Title = "Success!", Message = "Your changes have been saved successfully. The bot configuration is now active." },
            new() { Variant = AlertVariant.Warning, Title = "Warning", Message = "Your Discord token is about to expire. Please update it in the next 7 days." },
            new() { Variant = AlertVariant.Error, Title = "Error", Message = "Failed to connect to Discord API. Please check your internet connection and try again." }
        };

        // Dismissible Alerts
        AlertDismissible = new List<AlertViewModel>
        {
            new() { Variant = AlertVariant.Info, Message = "This alert can be dismissed by clicking the X button.", IsDismissible = true },
            new() { Variant = AlertVariant.Success, Title = "Welcome!", Message = "You've successfully logged into the Discord Bot Admin panel.", IsDismissible = true }
        };
    }

    private void InitializeConfirmationDialogs()
    {
        InfoConfirmation = new ConfirmationModalViewModel
        {
            Id = "showcase-info-modal",
            Title = "Information",
            Message = "This action will update your notification preferences. You can change this at any time from your settings.",
            ConfirmText = "Continue",
            CancelText = "Cancel",
            Variant = ConfirmationVariant.Info,
            FormAction = null,
            FormHandler = "ShowcaseConfirm"
        };

        WarningConfirmation = new ConfirmationModalViewModel
        {
            Id = "showcase-warning-modal",
            Title = "Are you sure?",
            Message = "This will reset the bot configuration for this server to default values. Custom command prefixes and module settings will be lost.",
            ConfirmText = "Reset Configuration",
            CancelText = "Keep Current",
            Variant = ConfirmationVariant.Warning,
            FormAction = null,
            FormHandler = "ShowcaseConfirm"
        };

        DangerConfirmation = new ConfirmationModalViewModel
        {
            Id = "showcase-danger-modal",
            Title = "Delete Server Data",
            Message = "This will permanently delete all stored data for this server including audit logs, command history, and custom settings. This action cannot be undone.",
            ConfirmText = "Delete Everything",
            CancelText = "Cancel",
            Variant = ConfirmationVariant.Danger,
            FormAction = null,
            FormHandler = "ShowcaseConfirm"
        };

        TypedConfirmation = new TypedConfirmationModalViewModel
        {
            Id = "showcase-typed-modal",
            Title = "Confirm Account Deletion",
            Message = "This will permanently delete your account and all associated data. This action is irreversible.",
            RequiredText = "DELETE",
            InputLabel = "Type DELETE to confirm",
            ConfirmText = "Delete My Account",
            CancelText = "Cancel",
            Variant = ConfirmationVariant.Danger,
            FormAction = null,
            FormHandler = "ShowcaseConfirm"
        };
    }

    private void InitializeCards()
    {
        // Default Card
        DefaultCard = new CardViewModel
        {
            Title = "Default Card",
            Subtitle = "Standard bordered card",
            BodyContent = "<p class='text-text-secondary'>This is a default card with a title, subtitle, and body content. It has a subtle border and no shadow.</p>",
            Variant = CardVariant.Default
        };

        // Elevated Card
        ElevatedCard = new CardViewModel
        {
            Title = "Elevated Card",
            Subtitle = "Card with shadow",
            BodyContent = "<p class='text-text-secondary'>This card has a shadow to create depth and visual hierarchy. Perfect for highlighting important content.</p>",
            Variant = CardVariant.Elevated
        };

        // Interactive Card
        InteractiveCard = new CardViewModel
        {
            Title = "Interactive Card",
            BodyContent = "<p class='text-text-secondary'>This card responds to hover states and can be clicked. Try hovering over it!</p>",
            IsInteractive = true,
            Variant = CardVariant.Elevated
        };

        // Collapsible Card
        CollapsibleCard = new CardViewModel
        {
            Title = "Collapsible Card",
            BodyContent = "<p class='text-text-secondary'>This card can be expanded or collapsed to save space. Click the header to toggle the content visibility.</p>",
            IsCollapsible = true,
            IsExpanded = true,
            Variant = CardVariant.Default
        };
    }

    private void InitializeEmptyStates()
    {
        EmptyStateTypes = new List<EmptyStateViewModel>
        {
            new()
            {
                Type = EmptyStateType.NoData,
                Title = "No Commands Yet",
                Description = "You haven't created any custom commands. Get started by creating your first command.",
                PrimaryActionText = "Create Command",
                PrimaryActionUrl = "#"
            },
            new()
            {
                Type = EmptyStateType.NoResults,
                Title = "No Results Found",
                Description = "We couldn't find any servers matching your search criteria. Try adjusting your filters.",
                PrimaryActionText = "Clear Filters",
                SecondaryActionText = "View All"
            },
            new()
            {
                Type = EmptyStateType.FirstTime,
                Title = "Welcome to Discord Bot Admin!",
                Description = "Let's get you started by connecting your first Discord bot. Follow our quick setup guide.",
                PrimaryActionText = "Start Setup",
                Size = EmptyStateSize.Large
            },
            new()
            {
                Type = EmptyStateType.Error,
                Title = "Failed to Load Data",
                Description = "There was an error loading the requested information. Please try again later.",
                PrimaryActionText = "Retry",
                Size = EmptyStateSize.Default
            },
            new()
            {
                Type = EmptyStateType.NoPermission,
                Title = "Access Denied",
                Description = "You don't have permission to view this content. Contact your server administrator.",
                Size = EmptyStateSize.Default
            }
        };
    }

    private void InitializePagination()
    {
        PaginationStyles = new List<PaginationViewModel>
        {
            new()
            {
                CurrentPage = 3,
                TotalPages = 10,
                TotalItems = 247,
                PageSize = 25,
                Style = PaginationStyle.Full,
                ShowItemCount = true,
                BaseUrl = "/components"
            },
            new()
            {
                CurrentPage = 2,
                TotalPages = 5,
                Style = PaginationStyle.Simple,
                BaseUrl = "/components"
            },
            new()
            {
                CurrentPage = 4,
                TotalPages = 8,
                Style = PaginationStyle.Compact,
                BaseUrl = "/components"
            },
            new()
            {
                CurrentPage = 1,
                TotalPages = 6,
                PageSize = 10,
                Style = PaginationStyle.Bordered,
                ShowPageSizeSelector = true,
                BaseUrl = "/components"
            }
        };
    }

    private void InitializeInteractionPrimitives()
    {
        InputAttributeExamples = new List<FormInputViewModel>
        {
            new() { Id = "attr-email", Name = "attr-email", Label = "Email (type=email, autocomplete=email)", Type = "email", Autocomplete = "email", Placeholder = "name@example.com", HelpText = "Shows the email keyboard on phones." },
            new() { Id = "attr-new-password", Name = "attr-new-password", Label = "New password (autocomplete=new-password)", Type = "password", Autocomplete = "new-password", IsRequired = true, HelpText = "Stops browsers from filling the current password here." },
            new() { Id = "attr-number", Name = "attr-number", Label = "Amount (number, min 0, max 1000, step 0.01)", Type = "number", Min = "0", Max = "1000", Step = "0.01", InputMode = "decimal", Value = "12.50", HelpText = "A decimal step passes through unchanged." },
            new() { Id = "attr-numeric-id", Name = "attr-numeric-id", Label = "Server ID (text, inputmode=numeric)", Type = "text", InputMode = "numeric", Pattern = "[0-9]{17,20}", Placeholder = "123456789012345678", HelpText = "IDs are text, not numbers: they are too large for a number field." },
            new() { Id = "attr-describedby", Name = "attr-describedby", Label = "Extra description (DescribedBy)", Type = "text", DescribedBy = "attr-describedby-note", HelpText = "Both this help text and the note below are read with the field." },
            new() { Id = "attr-counter", Name = "attr-counter", Label = "With character count", Type = "text", MaxLength = 40, ShowCharacterCount = true, Value = "Starting text" },
            new() { Id = "attr-required", Name = "attr-required", Label = "Required", IsRequired = true, Placeholder = "Required field" },
            new() { Id = "attr-readonly", Name = "attr-readonly", Label = "Read only", Value = "Cannot be edited", IsReadOnly = true },
            new() { Id = "attr-disabled", Name = "attr-disabled", Label = "Disabled", Value = "Disabled field", IsDisabled = true }
        };

        TextareaStates = new List<FormTextareaViewModel>
        {
            new() { Id = "ta-default", Name = "ta-default", Label = "Message", Placeholder = "Write something", HelpText = "Resizes vertically.", Dir = "auto" },
            new() { Id = "ta-required", Name = "ta-required", Label = "Reason", IsRequired = true, Rows = 3, Value = "Needed for the audit log." },
            new() { Id = "ta-error", Name = "ta-error", Label = "Description", ValidationState = ValidationState.Error, ValidationMessage = "Description is required.", Rows = 3 },
            new() { Id = "ta-warning", Name = "ta-warning", Label = "Notes", ValidationState = ValidationState.Warning, ValidationMessage = "This is longer than most people read.", Value = "A long note.", Rows = 3 },
            new() { Id = "ta-max", Name = "ta-max", Label = "Limited to 200 characters", MaxLength = 200, Rows = 3, HelpText = "The browser stops typing at the limit." },
            new() { Id = "ta-readonly", Name = "ta-readonly", Label = "Read only", Value = "Cannot be edited.", IsReadOnly = true, Rows = 2 },
            new() { Id = "ta-disabled", Name = "ta-disabled", Label = "Disabled", Value = "Disabled.", IsDisabled = true, Rows = 2 }
        };

        RadioCardsDefault = new RadioCardGroupViewModel
        {
            Name = "rc-mode",
            Legend = "Purge mode",
            HelpText = "Arrow keys move between cards; Tab leaves the group.",
            SelectedValue = "recent",
            IsRequired = true,
            Options = new List<RadioCardViewModel>
            {
                new() { Value = "recent", Title = "Recent messages", Description = "The last 100 messages in a channel." },
                new() { Value = "user", Title = "By user", Description = "Everything one member has posted." },
                new() { Value = "all", Title = "Entire channel", Description = "Not available while the bot is offline.", IsDisabled = true }
            }
        };

        RadioCardsIcons = new RadioCardGroupViewModel
        {
            Name = "rc-icons",
            Legend = "Notification channel",
            Columns = 3,
            Options = new List<RadioCardViewModel>
            {
                new() { Value = "email", Title = "Email", Description = "A message to your inbox.", IconPath = "M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" },
                new() { Value = "push", Title = "Push", Description = "A phone notification.", IconPath = "M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" },
                new() { Value = "none", Title = "None", Description = "Check the portal yourself.", IconPath = "M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" }
            }
        };

        RadioCardsInvalid = new RadioCardGroupViewModel
        {
            Name = "rc-invalid",
            Legend = "Retention period",
            IsRequired = true,
            ValidationMessage = "Choose how long to keep the data.",
            Options = new List<RadioCardViewModel>
            {
                new() { Value = "30", Title = "30 days" },
                new() { Value = "90", Title = "90 days" }
            }
        };

        ToggleStates = new List<FormToggleViewModel>
        {
            new() { Id = "tg-on", Name = "tg-on", Label = "On", IsChecked = true },
            new() { Id = "tg-off", Name = "tg-off", Label = "Off", IsChecked = false },
            new() { Id = "tg-desc", Name = "tg-desc", Label = "With a description", Description = "Read as the switch's description by screen readers.", IsChecked = true },
            new() { Id = "tg-disabled-on", Name = "tg-disabled-on", Label = "Disabled, on", IsChecked = true, IsDisabled = true },
            new() { Id = "tg-disabled-off", Name = "tg-disabled-off", Label = "Disabled, off", IsDisabled = true }
        };

        ToggleFormDemo = new List<FormToggleViewModel>
        {
            new() { Id = "demo-notify", Name = "Notify", Label = "Notify me", Description = "Posts true when on, false when off.", IsChecked = true },
            new() { Id = "demo-digest", Name = "Digest", Label = "Weekly digest", Description = "Starts off, so a plain checkbox would post nothing.", IsChecked = false }
        };

        // Redirect and failure paths of the static confirmation modal
        RedirectConfirmation = new ConfirmationModalViewModel
        {
            Id = "showcase-redirect-modal",
            Title = "Reset password",
            Message = "This handler answers with a redirect and a TempData toast, like Users/Edit. The page reloads once and shows the toast.",
            ConfirmText = "Reset password",
            Variant = ConfirmationVariant.Warning,
            FormHandler = "ShowcaseRedirect"
        };

        FailingConfirmation = new ConfirmationModalViewModel
        {
            Id = "showcase-fail-modal",
            Title = "An action that fails",
            Message = "The server answers with an error. The modal stays open, the button comes back, and an error toast explains.",
            ConfirmText = "Try it",
            Variant = ConfirmationVariant.Danger,
            FormHandler = "ShowcaseFail"
        };

        const string demoUrl = "/Components?tab=pagination";
        PaginationStates = new List<(string, string, PaginationViewModel)>
        {
            ("Middle page", "First, last, ellipses and the item range.",
                new() { CurrentPage = 5, TotalPages = 12, TotalItems = 287, PageSize = 25, ShowItemCount = true, BaseUrl = demoUrl, AriaLabel = "Middle page example" }),
            ("First page", "Previous and First are disabled spans, not links.",
                new() { CurrentPage = 1, TotalPages = 4, TotalItems = 90, PageSize = 25, ShowItemCount = true, BaseUrl = demoUrl, AriaLabel = "First page example" }),
            ("Last page", "Next and Last are disabled; the range ends at the total.",
                new() { CurrentPage = 4, TotalPages = 4, TotalItems = 90, PageSize = 25, ShowItemCount = true, BaseUrl = demoUrl, AriaLabel = "Last page example" }),
            ("Few pages", "Up to 7 pages show every number.",
                new() { CurrentPage = 2, TotalPages = 5, TotalItems = 120, PageSize = 25, ShowItemCount = true, BaseUrl = demoUrl, AriaLabel = "Few pages example" }),
            ("Page size selector", "A GET form that keeps the other filters in the address; no script text carries the URL.",
                new() { CurrentPage = 1, TotalPages = 3, TotalItems = 62, PageSize = 25, ShowItemCount = true, ShowPageSizeSelector = true, BaseUrl = "/Components?search=a'b&role=admin", AriaLabel = "Page size example" }),
            ("Single page", "One page of results: the count stays, there is nothing to navigate.",
                new() { CurrentPage = 1, TotalPages = 1, TotalItems = 7, PageSize = 25, ShowItemCount = true, BaseUrl = demoUrl, AriaLabel = "Single page example" }),
            ("No results", "Says so in words, never a range that ends before it starts.",
                new() { CurrentPage = 1, TotalPages = 0, TotalItems = 0, PageSize = 25, ShowItemCount = true, ShowPageSizeSelector = true, BaseUrl = demoUrl, AriaLabel = "No results example" }),
            ("Total unknown", "Callers that do not count items get \"Page X of Y\" instead of a made-up range.",
                new() { CurrentPage = 2, TotalPages = 6, ShowItemCount = true, BaseUrl = demoUrl, AriaLabel = "Unknown total example" }),
            ("Page past the end", "A stale link to page 9 of 4 shows the last page, not a range that runs backwards.",
                new() { CurrentPage = 9, TotalPages = 4, TotalItems = 90, PageSize = 25, ShowItemCount = true, BaseUrl = demoUrl, AriaLabel = "Past the end example" })
        };

        EmptyStateVariants = new List<(string, EmptyStateViewModel)>
        {
            ("Custom icon", new EmptyStateViewModel
            {
                Type = EmptyStateType.NoData,
                Size = EmptyStateSize.Compact,
                Title = "Nothing scheduled",
                Description = "Scheduled messages you create will appear here.",
                IconSvgPath = "M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z",
                PrimaryActionText = "Schedule a message",
                PrimaryActionUrl = "#"
            }),
            ("Filtered empty: link action with no icon", new EmptyStateViewModel
            {
                Type = EmptyStateType.NoResults,
                Size = EmptyStateSize.Compact,
                Title = "No users match your filters",
                Description = "Try different filters, or clear them to see everyone.",
                PrimaryActionText = "Clear filters",
                PrimaryActionUrl = "/Components",
                PrimaryActionIconPath = ""
            }),
            ("Error with Retry (announced, data attribute hook)", new EmptyStateViewModel
            {
                Type = EmptyStateType.Error,
                Size = EmptyStateSize.Compact,
                Title = "Could not load the log",
                Description = "Something went wrong while loading. Check your connection and try again.",
                PrimaryActionText = "Retry",
                PrimaryActionIconPath = "",
                PrimaryActionAttributes = new Dictionary<string, string> { ["data-action"] = "retry-demo" },
                Announce = true,
                HeadingLevel = 4
            }),
            ("Offline", new EmptyStateViewModel
            {
                Type = EmptyStateType.Offline,
                Size = EmptyStateSize.Compact,
                Title = "You are offline",
                Description = "Reconnect to see live data."
            })
        };
    }

    private void InitializeNavTabs()
    {
        // Underline Style (Default)
        UnderlineTabs = new NavTabsViewModel
        {
            Tabs = new List<NavTabItem>
            {
                new() { Id = "overview", Label = "Overview" },
                new() { Id = "details", Label = "Details" },
                new() { Id = "settings", Label = "Settings" }
            },
            ActiveTabId = "overview",
            StyleVariant = NavTabStyle.Underline,
            NavigationMode = NavMode.InPage,
            PersistenceMode = NavPersistence.None,
            ContainerId = "underlineTabs",
            AriaLabel = "Underline style demo"
        };

        // Pills Style
        PillsTabs = new NavTabsViewModel
        {
            Tabs = new List<NavTabItem>
            {
                new() { Id = "general", Label = "General" },
                new() { Id = "advanced", Label = "Advanced" },
                new() { Id = "security", Label = "Security" }
            },
            ActiveTabId = "general",
            StyleVariant = NavTabStyle.Pills,
            NavigationMode = NavMode.InPage,
            PersistenceMode = NavPersistence.None,
            ContainerId = "pillsTabs",
            AriaLabel = "Pills style demo"
        };

        // Bordered Style
        BorderedTabs = new NavTabsViewModel
        {
            Tabs = new List<NavTabItem>
            {
                new() { Id = "profile", Label = "Profile" },
                new() { Id = "preferences", Label = "Preferences" },
                new() { Id = "notifications", Label = "Notifications" }
            },
            ActiveTabId = "profile",
            StyleVariant = NavTabStyle.Bordered,
            NavigationMode = NavMode.InPage,
            PersistenceMode = NavPersistence.None,
            ContainerId = "borderedTabs",
            AriaLabel = "Bordered style demo"
        };

        // Icons Demo (with outline/solid swap)
        IconTabs = new NavTabsViewModel
        {
            Tabs = new List<NavTabItem>
            {
                new()
                {
                    Id = "home",
                    Label = "Home",
                    IconPathOutline = "M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6",
                },
                new()
                {
                    Id = "users",
                    Label = "Members",
                    ShortLabel = "Users",
                    IconPathOutline = "M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z",
                },
                new()
                {
                    Id = "cog",
                    Label = "Configuration",
                    ShortLabel = "Config",
                    IconPathOutline = "M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z",
                }
            },
            ActiveTabId = "home",
            StyleVariant = NavTabStyle.Pills,
            NavigationMode = NavMode.InPage,
            PersistenceMode = NavPersistence.Hash,
            ContainerId = "iconTabs",
            AriaLabel = "Navigation with icons"
        };
    }
}
