using BookingSystem.Shared.Messaging;

namespace BookingSystem.BookingService.Application.Exceptions;

// A missing aggregate is a permanent failure: redelivering the same event can never make it appear.
public class NotFoundException(string message) : Exception(message), IPermanentMessageException;
