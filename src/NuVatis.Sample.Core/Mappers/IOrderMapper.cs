/**
 * 주문 매퍼 인터페이스
 * XML 파일로 복잡한 JOIN 쿼리 및 트랜잭션 처리 예제
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */

using NuVatis.Sample.Core.Models;

namespace NuVatis.Sample.Core.Mappers;

public interface IOrderMapper
{
    /// <summary>
    /// ID로 주문 조회 (사용자 정보 포함)
    /// </summary>
    Order? GetById(int id);

    /// <summary>
    /// ID로 주문 조회 (주문 상세 및 상품 정보 포함)
    /// ResultMap의 collection 활용 예제
    /// </summary>
    Order? GetByIdWithItems(int id);

    /// <summary>
    /// 사용자별 주문 목록 조회
    /// </summary>
    IList<Order> GetByUserId(int userId);

    /// <summary>
    /// 상태별 주문 목록 조회
    /// </summary>
    IList<Order> GetByStatus(string status);

    /// <summary>
    /// 주문 등록
    /// </summary>
    int Insert(Order order);

    /// <summary>
    /// 주문 상세 등록
    /// </summary>
    int InsertItem(OrderItem item);

    /// <summary>
    /// 주문 상태 업데이트
    /// </summary>
    int UpdateStatus(int id, string status);

    /// <summary>
    /// 주문 삭제
    /// </summary>
    int Delete(int id);

    /// <summary>
    /// 주문 상세 삭제
    /// </summary>
    int DeleteItems(int orderId);
}
