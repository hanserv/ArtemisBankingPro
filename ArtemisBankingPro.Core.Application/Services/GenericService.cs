using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Interfaces;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Core.Application.Services
{
    public class GenericService<TEntity, TDto, TCreateDto, TUpdateDto> : IGenericService<TDto, TCreateDto, TUpdateDto>
        where TEntity : class
        where TDto : class
        where TCreateDto : class
        where TUpdateDto : class
    {
        protected readonly IGenericRepository<TEntity> _repository;
        protected readonly IMapper _mapper;

        public GenericService(IGenericRepository<TEntity> repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public virtual async Task<Result<IEnumerable<TDto>>> GetAllAsync()
        {
            var dtos = await _repository.GetAllQuery().ProjectToType<TDto>(_mapper.Config).ToListAsync();

            return Result<IEnumerable<TDto>>.Success(dtos);
        }

        public virtual async Task<Result<TDto>> GetByIdAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);

            if (entity is null)
            {
                return Result<TDto>.Failure(error: "The record was not found.");
            }

            return Result<TDto>.Success(_mapper.Map<TDto>(entity));
        }

        public virtual async Task<Result<TDto>> AddAsync(TCreateDto createDto)
        {
            var entity = _mapper.Map<TEntity>(createDto);

            var created = await _repository.AddAsync(entity);

            return Result<TDto>.Success(_mapper.Map<TDto>(created));
        }

        public virtual async Task<Result<TDto>> UpdateAsync(int id, TUpdateDto updateDto)
        {
            var entity = await _repository.GetByIdAsync(id);

            if (entity is null)
            {
                return Result<TDto>.Failure(error: "The record was not found.");
            }

            _mapper.Map(updateDto, entity);
            await _repository.UpdateAsync(entity);

            return Result<TDto>.Success(_mapper.Map<TDto>(entity));
        }

        public virtual async Task<Result> DeleteAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity is null)
            {
                return Result.Failure(error: "The record was not found.");
            }

            await _repository.DeleteAsync(entity);

            return Result.Success(message: "The record was deleted successfully.");
        }
    }
}
