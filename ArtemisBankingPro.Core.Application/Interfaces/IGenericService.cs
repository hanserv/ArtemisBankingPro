namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface IGenericService<TDto, TCreateDto, TUpdateDto>
    {
        Task<Result<IEnumerable<TDto>>> GetAllAsync();
        Task<Result<TDto>> GetByIdAsync(int id);
        Task<Result<TDto>> AddAsync(TCreateDto createDto);
        Task<Result<TDto>> UpdateAsync(int id, TUpdateDto updateDto);
        Task<Result> DeleteAsync(int id);
    }
}
