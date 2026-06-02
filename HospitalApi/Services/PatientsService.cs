using HospitalApi.Infrastructure;
using HospitalApi.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Services;

public class PatientsService : IPatientsService
{
    private readonly MasterContext _context;

    private static readonly DateTime MaxDate =
        new(9999, 12, 31, 23, 59, 59);

    public PatientsService(MasterContext context)
    {
        _context = context;
    }

    public async Task<object> GetPatientsAsync(string? search)
    {
        var query = _context.Patients
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";

            query = query.Where(p =>
                EF.Functions.Like(p.FirstName, pattern) ||
                EF.Functions.Like(p.LastName, pattern));
        }

        var patients = await query
            .Select(p => new
            {
                pesel = p.Pesel,
                firstName = p.FirstName,
                lastName = p.LastName,
                age = p.Age,
                sex = p.Sex ? "Male" : "Female",

                admissions = p.Admissions.Select(a => new
                {
                    id = a.Id,
                    admissionDate = a.AdmissionDate,
                    dischargeDate = a.DischargeDate,
                    ward = new
                    {
                        id = a.Ward.Id,
                        name = a.Ward.Name,
                        description = a.Ward.Description
                    }
                }).ToList(),

                bedAssignments = p.BedAssignments.Select(ba => new
                {
                    id = ba.Id,
                    from = ba.From,
                    to = ba.To,
                    bed = new
                    {
                        id = ba.Bed.Id,
                        bedType = new
                        {
                            id = ba.Bed.BedType.Id,
                            name = ba.Bed.BedType.Name,
                            description = ba.Bed.BedType.Description
                        },
                        room = new
                        {
                            id = ba.Bed.Room.Id,
                            hasTv = ba.Bed.Room.HasTv,
                            ward = new
                            {
                                id = ba.Bed.Room.Ward.Id,
                                name = ba.Bed.Room.Ward.Name,
                                description = ba.Bed.Room.Ward.Description
                            }
                        }
                    }
                }).ToList()
            })
            .ToListAsync();

        return patients;
    }

    public async Task<(int StatusCode, object Body)> AssignBedAsync(
        string pesel,
        AssignBedRequest? request)
    {
        if (request is null)
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "Treść żądania jest wymagana.");
        }

        if (request.From is null)
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "Pole 'from' jest wymagane.");
        }

        if (request.To is not null && request.To <= request.From)
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "Pole 'to' musi być późniejsze niż pole 'from'.");
        }

        if (string.IsNullOrWhiteSpace(request.BedType))
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "Pole 'bedType' jest wymagane.");
        }

        if (string.IsNullOrWhiteSpace(request.Ward))
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "Pole 'ward' jest wymagane.");
        }

        var patientExists = await _context.Patients
            .AnyAsync(p => p.Pesel == pesel);

        if (!patientExists)
        {
            return Error(
                StatusCodes.Status404NotFound,
                $"Nie znaleziono pacjenta o numerze PESEL '{pesel}'.");
        }

        var bedTypeName = request.BedType.Trim();
        var wardName = request.Ward.Trim();

        var wardExists = await _context.Wards
            .AnyAsync(w => w.Name == wardName);

        if (!wardExists)
        {
            return Error(
                StatusCodes.Status404NotFound,
                $"Nie znaleziono oddziału '{wardName}'.");
        }

        var bedTypeExists = await _context.BedTypes
            .AnyAsync(bt => bt.Name == bedTypeName);

        if (!bedTypeExists)
        {
            return Error(
                StatusCodes.Status404NotFound,
                $"Nie znaleziono typu łóżka '{bedTypeName}'.");
        }

        var bedExistsInWard = await _context.Beds
            .AnyAsync(b =>
                b.BedType.Name == bedTypeName &&
                b.Room.Ward.Name == wardName);

        if (!bedExistsInWard)
        {
            return Error(
                StatusCodes.Status404NotFound,
                $"Na oddziale '{wardName}' nie ma łóżka typu '{bedTypeName}'.");
        }

        var requestedFrom = request.From.Value;
        var requestedTo = request.To ?? MaxDate;

        var freeBed = await _context.Beds
            .Where(b =>
                b.BedType.Name == bedTypeName &&
                b.Room.Ward.Name == wardName)
            .Where(b => !_context.BedAssignments.Any(ba =>
                ba.BedId == b.Id &&
                ba.From < requestedTo &&
                requestedFrom < (ba.To ?? MaxDate)))
            .OrderBy(b => b.Id)
            .FirstOrDefaultAsync();

        if (freeBed is null)
        {
            return Error(
                StatusCodes.Status404NotFound,
                $"Brak wolnego łóżka typu '{bedTypeName}' na oddziale '{wardName}' w podanym okresie.");
        }

        var assignment = new BedAssignment
        {
            PatientPesel = pesel,
            BedId = freeBed.Id,
            From = requestedFrom,
            To = request.To
        };

        _context.BedAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        var response = await _context.BedAssignments
            .AsNoTracking()
            .Where(ba => ba.Id == assignment.Id)
            .Select(ba => new
            {
                id = ba.Id,
                from = ba.From,
                to = ba.To,
                bed = new
                {
                    id = ba.Bed.Id,
                    bedType = new
                    {
                        id = ba.Bed.BedType.Id,
                        name = ba.Bed.BedType.Name,
                        description = ba.Bed.BedType.Description
                    },
                    room = new
                    {
                        id = ba.Bed.Room.Id,
                        hasTv = ba.Bed.Room.HasTv,
                        ward = new
                        {
                            id = ba.Bed.Room.Ward.Id,
                            name = ba.Bed.Room.Ward.Name,
                            description = ba.Bed.Room.Ward.Description
                        }
                    }
                }
            })
            .FirstAsync();

        return (StatusCodes.Status201Created, response);
    }

    private static (int StatusCode, object Body) Error(int statusCode, string message)
    {
        return (statusCode, new { message });
    }
}