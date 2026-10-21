namespace Staybnb.Models;

public enum BookingStatus { Pending, Approved, Rejected, CheckedIn, Cancelled }
public enum PaymentStatus { Pending, Paid, Refunded }
public enum ApplicationStatus { Pending, Approved, Rejected }
public enum ActivityType { Login, RoleChange, BookingUpdate, Application, Property, System }
public enum NotificationType { BookingApproved, BookingRejected, NewMessage, ApplicationUpdate, General }
public enum CheckInStatus { NotStarted, InProgress, Submitted, Verified, Rejected }
public enum DocumentStatus { Pending, Verified, Rejected }