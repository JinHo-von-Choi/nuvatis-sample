/**
 * 사용자 매퍼 인터페이스
 * XML 파일로 SQL을 관리하는 방식 예제
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */

using NuVatis.Sample.Core.Models;

namespace NuVatis.Sample.Core.Mappers;

public interface IUserMapper
{
    /// <summary>
    /// ID로 사용자 조회
    /// </summary>
    User? GetById(int id);

    /// <summary>
    /// ID로 사용자 조회 (비동기)
    /// </summary>
    Task<User?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// 모든 사용자 조회
    /// </summary>
    IList<User> GetAll();

    /// <summary>
    /// 동적 검색 (이름, 이메일, ID 목록)
    /// MyBatis 동적 SQL 예제 (if, foreach, where)
    /// </summary>
    IList<User> Search(UserSearchParam param);

    /// <summary>
    /// 페이징 검색
    /// </summary>
    Task<IList<User>> SearchWithPagingAsync(UserSearchParam param, CancellationToken ct = default);

    /// <summary>
    /// 사용자 수 조회
    /// </summary>
    int Count(UserSearchParam param);

    /// <summary>
    /// 사용자 등록
    /// </summary>
    int Insert(User user);

    /// <summary>
    /// 사용자 수정
    /// </summary>
    int Update(User user);

    /// <summary>
    /// 사용자 삭제 (Soft Delete)
    /// </summary>
    int SoftDelete(int id);

    /// <summary>
    /// 사용자 삭제 (Hard Delete)
    /// </summary>
    int Delete(int id);
}
