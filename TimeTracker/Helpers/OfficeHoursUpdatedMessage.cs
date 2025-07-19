using CommunityToolkit.Mvvm.Messaging.Messages;

namespace TimeTracker.Helpers
{
    public class OfficeHoursUpdatedMessage : ValueChangedMessage<double>
    {
        public OfficeHoursUpdatedMessage(double newHours) : base(newHours) { }
    }
}
