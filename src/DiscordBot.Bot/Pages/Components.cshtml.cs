// src/DiscordBot.Bot/Pages/Components.cshtml.cs
using Microsoft.AspNetCore.Authorization;
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

    // Cards
    public CardViewModel DefaultCard { get; set; } = new();
    public CardViewModel ElevatedCard { get; set; } = new();
    public CardViewModel InteractiveCard { get; set; } = new();
    public CardViewModel CollapsibleCard { get; set; } = new();

    // Empty States
    public List<EmptyStateViewModel> EmptyStateTypes { get; set; } = new();

    // Pagination
    public List<PaginationViewModel> PaginationStyles { get; set; } = new();

    // NavTabs
    public NavTabsViewModel UnderlineTabs { get; set; } = default!;
    public NavTabsViewModel PillsTabs { get; set; } = default!;
    public NavTabsViewModel BorderedTabs { get; set; } = default!;
    public NavTabsViewModel IconTabs { get; set; } = default!;

    public void OnGet()
    {
        InitializeButtons();
        InitializeBadges();
        InitializeStatusIndicators();
        InitializeLoadingSpinners();
        InitializeFormInputs();
        InitializeFormSelects();
        InitializeAlerts();
        InitializeCards();
        InitializeEmptyStates();
        InitializePagination();
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
