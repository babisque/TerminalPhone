namespace TerminalPhone.Application.DTOs;

public record CommandResultDto(
    string Alias,
    string Output,
    bool Success,
    DateTime ExecutedAt
);