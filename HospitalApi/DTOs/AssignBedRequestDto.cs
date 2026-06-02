namespace HospitalApi.DTOs;

public class AssignBedRequestDto
{
    public DateTime From { get; set; }
    public DateTime? To { get; set; }

    public string? BedType { get; set; }
    public string? Ward { get; set; }
}