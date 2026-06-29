namespace BookingSystem.Shared.Messaging;

/// <summary>
/// Marks an exception as a permanent (poison) failure: retrying the same message can never
/// succeed. <see cref="KafkaConsumerBase{T}"/> routes these straight to the dead-letter topic
/// instead of consuming the bounded retry budget reserved for transient faults.
/// </summary>
public interface IPermanentMessageException;
