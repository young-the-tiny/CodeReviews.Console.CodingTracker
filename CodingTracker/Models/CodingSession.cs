namespace CodingTracker.Models;

record CodingSession(int Id, DateTime StartTime, DateTime EndTime)
{
    public TimeSpan Duration => EndTime - StartTime;
}
