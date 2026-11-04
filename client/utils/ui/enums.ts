/**
 * Closed value sets of the Ui* kit.
 *
 * Every variant, size, tone and alignment a kit component accepts is an enum member, never a
 * bare literal. A component declares its prop as the template-literal form of the enum
 * (`${UiSize}`) so a screen may still write `size="sm"` while a script uses `UiSize.Sm`;
 * both resolve to the same string.
 *
 * @module utils/ui/enums
 */

/** Three-step scale shared by controls (button, input, select, badge, avatar). */
export enum UiSize {
  /** Compact control. */
  Sm = 'sm',
  /** Default control. */
  Md = 'md',
  /** Large control. */
  Lg = 'lg',
}

/** Edge steps of a `UiTile`: a count badge up to a large icon square. */
export enum UiTileSize {
  /** A 20px count badge. */
  Sm = 'sm',
  /** A 32px tile. */
  Md = 'md',
  /** A 40px tile. */
  Lg = 'lg',
  /** A 48px tile. */
  Xl = 'xl',
}

/** Visual weight of a button. */
export enum UiButtonVariant {
  /** Filled brand button, the main action. */
  Primary = 'primary',
  /** Filled neutral button. */
  Secondary = 'secondary',
  /** Brand outline. */
  Outline = 'outline',
  /** Text-only, no frame. */
  Ghost = 'ghost',
  /** Quiet framed button. */
  Subtle = 'subtle',
  /** Destructive action. */
  Danger = 'danger',
  /** Structure and focus only: the screen paints it (a template with its own palette). */
  Bare = 'bare',
}

/** Colour family of a badge, stat tile or tone-aware surface. */
export enum UiTone {
  /** Neutral grey. */
  Default = 'default',
  /** Brand colour. */
  Primary = 'primary',
  /** Accent colour. */
  Secondary = 'secondary',
  /** Positive outcome. */
  Success = 'success',
  /** Needs attention. */
  Warning = 'warning',
  /** Failure or destructive. */
  Danger = 'danger',
  /** Informational. */
  Info = 'info',
}

/** Meaning of an alert banner. */
export enum UiAlertVariant {
  /** Needs attention. */
  Warning = 'warning',
  /** Something failed. */
  Error = 'error',
  /** Neutral information. */
  Info = 'info',
  /** Something worked. */
  Success = 'success',
}

/** Live-region role an alert banner announces itself with. */
export enum UiAlertRole {
  /** Assertive: read out at once, for errors and warnings. */
  Alert = 'alert',
  /** Polite: read out when the reader is idle, for information and success. */
  Status = 'status',
}

/** Inner padding of a card. */
export enum UiCardPadding {
  /** No padding, content goes edge to edge. */
  None = 'none',
  /** 1rem. */
  Sm = 'sm',
  /** 1.25rem. */
  Md = 'md',
  /** 1.5rem. */
  Lg = 'lg',
  /** 2rem. */
  Xl = 'xl',
}

/** Surface treatment of a card. */
export enum UiCardTone {
  /** Plain framed surface. */
  Default = 'default',
  /** Radius, border width and padding only: the screen paints background and border colour (a template with its own palette). */
  Bare = 'bare',
}

/** Vertical rhythm between the fields of a form. */
export enum UiFormSpacing {
  /** No wrapper spacing; the caller lays fields out. */
  None = 'none',
  /** 0.75rem. */
  Sm = 'sm',
  /** 1rem. */
  Md = 'md',
  /** 1.25rem. */
  Lg = 'lg',
}

/** Horizontal text alignment of a table cell. */
export enum UiAlign {
  /** Start-aligned. */
  Left = 'left',
  /** Centred. */
  Center = 'center',
  /** End-aligned. */
  Right = 'right',
}

/** Width of a dialog or drawer panel. */
export enum UiPanelSize {
  /** Narrow. */
  Sm = 'sm',
  /** Default. */
  Md = 'md',
  /** Wide. */
  Lg = 'lg',
  /** Extra wide. */
  Xl = 'xl',
  /** Widest. */
  Full = 'full',
}

/** Edge a drawer slides in from. */
export enum UiDrawerSide {
  /** Left edge. */
  Left = 'left',
  /** Right edge. */
  Right = 'right',
}

/** Look of a UiLink. */
export enum UiLinkTone {
  /** Brand-coloured link. */
  Primary = 'primary',
  /** Grey link that darkens on hover. */
  Muted = 'muted',
  /** Takes the colour of the surrounding text. */
  Inherit = 'inherit',
}

/** Look of a UiIconButton. */
export enum UiIconButtonVariant {
  /** Quiet framed square. */
  Subtle = 'subtle',
  /** No frame until hovered. */
  Ghost = 'ghost',
  /** Filled brand square. */
  Primary = 'primary',
  /** Destructive. */
  Danger = 'danger',
  /** Structure and focus only; the screen paints the frame and colours through its class. */
  Bare = 'bare',
}

/** How an image fills its box. */
export enum UiImageFit {
  /** Cover, cropping overflow. */
  Cover = 'cover',
  /** Contain, letterboxed. */
  Contain = 'contain',
}

/** Corner rounding of an image. */
export enum UiImageRounding {
  /** Square corners. */
  None = 'none',
  /** Rounded corners. */
  Md = 'md',
  /** Fully round. */
  Full = 'full',
}

/** Space between children of a stack, grid or split. */
export enum UiGap {
  /** No gap. */
  None = 'none',
  /** 0.5rem. */
  Xs = 'xs',
  /** 0.75rem. */
  Sm = 'sm',
  /** 1rem. */
  Md = 'md',
  /** 1.5rem. */
  Lg = 'lg',
  /** 2.5rem. */
  Xl = 'xl',
}

/** Axis of a stack. */
export enum UiDirection {
  /** Children stacked top to bottom. */
  Column = 'column',
  /** Children laid out side by side. */
  Row = 'row',
}

/** Cross-axis alignment of a stack. */
export enum UiCrossAlign {
  /** Align to the start. */
  Start = 'start',
  /** Centre. */
  Center = 'center',
  /** Align to the end. */
  End = 'end',
  /** Stretch to fill. */
  Stretch = 'stretch',
}

/** Main-axis distribution of a stack. */
export enum UiJustify {
  /** Pack at the start. */
  Start = 'start',
  /** Centre. */
  Center = 'center',
  /** Pack at the end. */
  End = 'end',
  /** Space between. */
  Between = 'between',
}

/** Responsive column preset of a grid. */
export enum UiGridColumns {
  /** One column. */
  One = '1',
  /** One column, two from `sm`. */
  Two = '2',
  /** One column, two from `sm`, three from `lg`. */
  Three = '3',
  /** Two columns, four from `lg`. */
  Four = '4',
  /** As many columns of at least 16rem as fit, so the count follows the available width. */
  Auto = 'auto',
}

/** Width ratio of the two halves of a split. */
export enum UiSplitRatio {
  /** Halves are equal. */
  Equal = 'equal',
  /** First half twice as wide. */
  StartWide = 'start-wide',
  /** Second half twice as wide. */
  EndWide = 'end-wide',
}

/** Vertical padding of a section. */
export enum UiSectionPadding {
  /** Tight. */
  Sm = 'sm',
  /** Default. */
  Md = 'md',
  /** Roomy. */
  Lg = 'lg',
}

/** Background of a section. */
export enum UiSectionTone {
  /** Page background. */
  Plain = 'plain',
  /** Slightly tinted band. */
  Muted = 'muted',
}

/** Semantic level of a heading. */
export enum UiHeadingLevel {
  /** h1. */
  H1 = 1,
  /** h2. */
  H2 = 2,
  /** h3. */
  H3 = 3,
  /** h4. */
  H4 = 4,
  /** h5. */
  H5 = 5,
  /** h6. */
  H6 = 6,
}

/** Visual size of a heading, independent of its semantic level. */
export enum UiHeadingSize {
  /** Hero title. */
  Display = 'display',
  /** Page title. */
  Title = 'title',
  /** Section title. */
  Section = 'section',
  /** Card title. */
  Subsection = 'subsection',
  /** Small uppercase label. */
  Label = 'label',
  /** No size and no colour: the screen supplies both (a template with its own type scale). */
  Bare = 'bare',
}

/** Size of body text. */
export enum UiTextSize {
  /** Extra small. */
  Xs = 'xs',
  /** Small. */
  Sm = 'sm',
  /** Body. */
  Md = 'md',
  /** Lead paragraph. */
  Lg = 'lg',
  /** No size utility: the screen supplies it. */
  Inherit = 'inherit',
}

/** Colour role of body text. */
export enum UiTextTone {
  /** Primary text colour. */
  Default = 'default',
  /** Secondary text colour. */
  Muted = 'muted',
  /** Tertiary text colour. */
  Subtle = 'subtle',
  /** Brand colour. */
  Primary = 'primary',
  /** Positive. */
  Success = 'success',
  /** Failure. */
  Danger = 'danger',
  /** No colour utility: the text takes the colour of its surroundings or of the screen's class. */
  Inherit = 'inherit',
}

/** Weight of body text. */
export enum UiTextWeight {
  /** Regular. */
  Normal = 'normal',
  /** Medium. */
  Medium = 'medium',
  /** Semibold. */
  Semibold = 'semibold',
  /** Bold. */
  Bold = 'bold',
}

/** Marker style of a UiList. */
export enum UiListStyle {
  /** No markers. */
  Plain = 'plain',
  /** Bullets (or numbers when ordered). */
  Marked = 'marked',
  /** Items separated by hairlines. */
  Divided = 'divided',
  /** No markers and no spacing: the screen lays the items out. */
  Bare = 'bare',
}

/** Shape of a loading placeholder. */
export enum UiSkeletonShape {
  /** One line of text. */
  Line = 'line',
  /** A rectangular block. */
  Block = 'block',
  /** A circle (avatar). */
  Circle = 'circle',
}

/** Named decorative backgrounds for marketing sections. */
export enum UiBackdropVariant {
  /** Two soft glows in the aurora palette. */
  Aurora = 'aurora',
  /** Vertical wash in the nova palette. */
  Nova = 'nova',
  /** Fine grid lines. */
  Grid = 'grid',
  /** Dot matrix. */
  Dots = 'dots',
}

/** Placement of a menu relative to its trigger. */
export enum UiPlacement {
  /** Opens below the trigger. */
  Bottom = 'bottom',
  /** Opens above the trigger. */
  Top = 'top',
}

/** Layout of the language switcher. */
export enum UiLanguageLayout {
  /** A trigger with a dropdown menu. */
  Dropdown = 'dropdown',
  /** Dropdown from `xl` up, a row of flag cards below. */
  Responsive = 'responsive',
}

/** Avatar diameter. */
export enum UiAvatarSize {
  /** Dense list. */
  Sm = 'sm',
  /** Header control. */
  Md = 'md',
}

/** Text type of an input. */
export enum UiInputType {
  /** Plain text. */
  Text = 'text',
  /** E-mail address. */
  Email = 'email',
  /** Password, with a reveal toggle. */
  Password = 'password',
  /** Number. */
  Number = 'number',
  /** Telephone. */
  Tel = 'tel',
  /** URL. */
  Url = 'url',
}

/** Native type of a button. */
export enum UiButtonType {
  /** Plain button. */
  Button = 'button',
  /** Submits its form. */
  Submit = 'submit',
  /** Resets its form. */
  Reset = 'reset',
}

/** Meaning of a confirm dialog. */
export enum UiConfirmVariant {
  /** Destructive action: bin icon, red confirm button. */
  Danger = 'danger',
  /** Needs attention: warning icon, primary confirm button. */
  Warning = 'warning',
}

/** Loading strategy of an image. */
export enum UiImageLoading {
  /** Load when near the viewport. */
  Lazy = 'lazy',
  /** Load immediately (above-the-fold images). */
  Eager = 'eager',
}

/** Direction a statistic moved. */
export enum UiStatTrend {
  /** Value went up. */
  Up = 'up',
  /** Value went down. */
  Down = 'down',
  /** Value did not change. */
  Flat = 'flat',
}

/** Look of a UiNavItem. */
export enum UiNavTone {
  /** Neutral menu entry. */
  Default = 'default',
  /** Entry that asks for action, drawn in the warning colour. */
  Warning = 'warning',
}

/** Which template palette a UiFooterLink takes its resting and hover colours from. */
export enum UiFooterLinkVariant {
  /** aurora tokens: muted text, accent on hover. */
  Aurora = 'aurora',
  /** nova tokens: muted text, brand on hover. */
  Nova = 'nova',
  /** classic: the classic surface tokens, which carry a light and a dark value each. */
  Classic = 'classic',
}

/** The kind of a node `parseRichText` produces, the discriminator `UiRichText` renders by. */
export enum UiRichNodeType {
  /** A decoded text run. */
  Text = 'text',
  /** An allow-listed element with children. */
  Element = 'element',
}

/** Which side of a conversation a chat bubble belongs to (`UiChatBubble`). */
export enum UiChatBubbleSide {
  /** Written by the reader: right-aligned, primary tint. */
  Own = 'own',
  /** Written by the other party: left-aligned, neutral. */
  Other = 'other',
}
