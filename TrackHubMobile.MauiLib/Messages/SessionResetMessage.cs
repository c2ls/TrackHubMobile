namespace TrackHubMobile.Messages;

// Follows the empty DataRefreshedMessage a sign-out publishes, so a screen can tell "nothing loaded
// yet" from a fleet that really is empty.
public sealed class SessionResetMessage;
