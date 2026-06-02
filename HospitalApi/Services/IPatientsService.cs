namespace HospitalApi.Services;

public class AssignBedRequest
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? BedType { get; set; }
    public string? Ward { get; set; }
}

public interface IPatientsService
{
    Task<object> GetPatientsAsync(string? search);

    Task<(int StatusCode, object Body)> AssignBedAsync(
        string pesel,
        AssignBedRequest? request);
}