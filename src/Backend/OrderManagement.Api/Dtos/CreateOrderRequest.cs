using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Api.Dtos;

public record CreateOrderRequest(
    [Required, MaxLength(200)] string Customer,
    [Required, MaxLength(200)] string Product,
    [Range(0.01, double.MaxValue)] decimal Amount);
