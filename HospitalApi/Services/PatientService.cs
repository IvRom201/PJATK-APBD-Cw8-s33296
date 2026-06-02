using HospitalApi.DTOs;
using HospitalApi.Infrastructure;
using HospitalApi.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Services;

public class PatientService : IPatientService
{
    private readonly MasterContext _context;

    public PatientService(MasterContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PatientDto>> GetPatientsAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var query = _context.Patients
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";

            query = query.Where(patient =>
                EF.Functions.Like(patient.FirstName, pattern) ||
                EF.Functions.Like(patient.LastName, pattern));
        }

        return await query
            .Select(patient => new PatientDto
            {
                Pesel = patient.Pesel,
                FirstName = patient.FirstName,
                LastName = patient.LastName,
                Age = patient.Age,
                Sex = patient.Sex ? "Male" : "Female",

                Admissions = patient.Admissions
                    .OrderBy(admission => admission.Id)
                    .Select(admission => new AdmissionDto
                    {
                        Id = admission.Id,
                        AdmissionDate = admission.AdmissionDate,
                        DischargeDate = admission.DischargeDate,
                        Ward = new WardDto
                        {
                            Id = admission.Ward.Id,
                            Name = admission.Ward.Name,
                            Description = admission.Ward.Description
                        }
                    })
                    .ToList(),

                BedAssignments = patient.BedAssignments
                    .OrderBy(assignment => assignment.Id)
                    .Select(assignment => new BedAssignmentDto
                    {
                        Id = assignment.Id,
                        From = assignment.From,
                        To = assignment.To,
                        Bed = new BedDto
                        {
                            Id = assignment.Bed.Id,
                            BedType = new BedTypeDto
                            {
                                Id = assignment.Bed.BedType.Id,
                                Name = assignment.Bed.BedType.Name,
                                Description = assignment.Bed.BedType.Description
                            },
                            Room = new RoomDto
                            {
                                Id = assignment.Bed.Room.Id,
                                HasTv = assignment.Bed.Room.HasTv,
                                Ward = new WardDto
                                {
                                    Id = assignment.Bed.Room.Ward.Id,
                                    Name = assignment.Bed.Room.Ward.Name,
                                    Description = assignment.Bed.Room.Ward.Description
                                }
                            }
                        }
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceResult<BedAssignmentDto>> AssignBedAsync(
        string pesel,
        AssignBedRequestDto request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateAssignBedRequest(request);

        if (validationError is not null)
        {
            return ServiceResult<BedAssignmentDto>.Failure(
                StatusCodes.Status400BadRequest,
                validationError
            );
        }

        var patientExists = await _context.Patients
            .AnyAsync(patient => patient.Pesel == pesel, cancellationToken);

        if (!patientExists)
        {
            return ServiceResult<BedAssignmentDto>.Failure(
                StatusCodes.Status404NotFound,
                $"Patient with PESEL '{pesel}' was not found."
            );
        }

        var bedTypeName = request.BedType!.Trim();
        var wardName = request.Ward!.Trim();

        var bedTypeExists = await _context.BedTypes
            .AnyAsync(bedType => bedType.Name == bedTypeName, cancellationToken);

        if (!bedTypeExists)
        {
            return ServiceResult<BedAssignmentDto>.Failure(
                StatusCodes.Status404NotFound,
                $"Bed type '{bedTypeName}' was not found."
            );
        }

        var wardExists = await _context.Wards
            .AnyAsync(ward => ward.Name == wardName, cancellationToken);

        if (!wardExists)
        {
            return ServiceResult<BedAssignmentDto>.Failure(
                StatusCodes.Status404NotFound,
                $"Ward '{wardName}' was not found."
            );
        }

        var requestedStart = request.From;
        var requestedEnd = request.To ?? new DateTime(9999, 12, 31, 23, 59, 59);

        var freeBed = await _context.Beds
            .Where(bed =>
                bed.BedType.Name == bedTypeName &&
                bed.Room.Ward.Name == wardName &&
                !bed.BedAssignments.Any(assignment =>
                    assignment.From < requestedEnd &&
                    (
                        assignment.To == null ||
                        requestedStart < assignment.To.Value
                    )))
            .OrderBy(bed => bed.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (freeBed is null)
        {
            return ServiceResult<BedAssignmentDto>.Failure(
                StatusCodes.Status404NotFound,
                $"No free bed of type '{bedTypeName}' was found in ward '{wardName}' for the selected period."
            );
        }

        var newAssignment = new BedAssignment
        {
            PatientPesel = pesel,
            BedId = freeBed.Id,
            From = request.From,
            To = request.To
        };

        _context.BedAssignments.Add(newAssignment);
        await _context.SaveChangesAsync(cancellationToken);

        var createdAssignment = await _context.BedAssignments
            .AsNoTracking()
            .Where(assignment => assignment.Id == newAssignment.Id)
            .Select(assignment => new BedAssignmentDto
            {
                Id = assignment.Id,
                From = assignment.From,
                To = assignment.To,
                Bed = new BedDto
                {
                    Id = assignment.Bed.Id,
                    BedType = new BedTypeDto
                    {
                        Id = assignment.Bed.BedType.Id,
                        Name = assignment.Bed.BedType.Name,
                        Description = assignment.Bed.BedType.Description
                    },
                    Room = new RoomDto
                    {
                        Id = assignment.Bed.Room.Id,
                        HasTv = assignment.Bed.Room.HasTv,
                        Ward = new WardDto
                        {
                            Id = assignment.Bed.Room.Ward.Id,
                            Name = assignment.Bed.Room.Ward.Name,
                            Description = assignment.Bed.Room.Ward.Description
                        }
                    }
                }
            })
            .SingleAsync(cancellationToken);

        return ServiceResult<BedAssignmentDto>.Success(
            createdAssignment,
            StatusCodes.Status201Created
        );
    }

    private static string? ValidateAssignBedRequest(AssignBedRequestDto request)
    {
        if (request.From == default)
        {
            return "Field 'from' is required.";
        }

        if (request.To is not null && request.To <= request.From)
        {
            return "Field 'to' must be later than field 'from'.";
        }

        if (string.IsNullOrWhiteSpace(request.BedType))
        {
            return "Field 'bedType' is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Ward))
        {
            return "Field 'ward' is required.";
        }

        return null;
    }
}