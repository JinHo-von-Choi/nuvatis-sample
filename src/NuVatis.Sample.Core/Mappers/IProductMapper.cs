/**
 * 상품 매퍼 인터페이스
 * XML 파일로 SQL 관리
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */

using NuVatis.Sample.Core.Models;

namespace NuVatis.Sample.Core.Mappers;

public interface IProductMapper
{
    /// <summary>
    /// ID로 상품 조회
    /// </summary>
    Product? GetById(int id);

    /// <summary>
    /// 모든 상품 조회
    /// </summary>
    IList<Product> GetAll();

    /// <summary>
    /// 카테고리별 상품 조회
    /// </summary>
    IList<Product> GetByCategory(string category);

    /// <summary>
    /// 상품 등록
    /// </summary>
    int Insert(Product product);

    /// <summary>
    /// 상품 수정
    /// </summary>
    int Update(Product product);

    /// <summary>
    /// 재고 수량 업데이트
    /// </summary>
    int UpdateStock(int productId, int quantity);

    /// <summary>
    /// 상품 삭제
    /// </summary>
    int Delete(int id);
}
