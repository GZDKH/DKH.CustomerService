# DKH.CustomerService -- gRPC API

## Привязка обратного вызова контакта получателя (Customer#54)

Существующие приватные Resolve/Revalidate в `DeliveryContactService` сохраняют обычные блокировки активной учётной записи, членства и связанного профиля Customer. Delivery проверяет принятую обязанность, назначение и того же исходного человека по своим данным, без повторного Customer RPC. Полная проверка физического исполнения остаётся отдельной операцией. Блокировки владельца не снимаются ради успешного вызова.

Добавленный ответ Delivery `acting_actor_ref` (поле9, Contracts1.18) передаёт приватные данные принятого действующего лица. Для человека профиль обязан совпасть с фактически заблокированным `CustomerProfile.Id` вызывающего, а не с CustomerAccount.Id, JWT subject или профилем получателя. Для компании используется точная нативная ссылка `counterparty.party` с ресурсом, ревизией и digest; Delivery независимо проверяет её полномочия. Отсутствующий, служебный, неизвестный или некорректный actor запрещён. Профиль получателя, контакт, согласие и весь контекст доставки проверяются отдельно; ссылка компании не превращается в профиль или новое разрешение.

Исходный проверенный человек, bearer, workspace, витрина и сроки токена/решения сохраняются до завершения вызова владельца, его освобождения и локальной транзакции. Поздняя смена контекста, отмена или истечение срока запрещают ответ. Это не отменяет уже зафиксированную квитанцию и не продлевает её срок. Проверки текущего адреса, согласия, точного повтора и отзыва обязательны.

Проверка исходников от 26.10.2026 использует изолированный полный DEVELOPMENT-пакет Delivery1.18 от владельца. Подлинная публикация пакета, независимые ревью, полный обязательный прогон, main/deploy и совместная приёмка V6.7 остаются отдельными этапами. Активация полномочий и полная готовность доставки не утверждаются. [Требования](../../openspec/changes/break-recipient-contact-callback-cycle/specs/recipient-contact-callback/spec.md).

> Translation Pending — см. [английскую версию](../grpc-api.md)

## Обзор

DKH.CustomerService предоставляет 10 gRPC-сервисов на порту **5010** (HTTP/2).

## CustomerAccountAdminService

**Пакет:** `proto.customer.api.customer_account_admin.v1`
**Версия Contracts:** `1.9.0`
**Авторизация:** только platform-роли `SuperAdmin`, realm `Admin` или `FullAccess`.

Сервис отделяет platform-global операции над аккаунтом от merchant-операций над
membership конкретной витрины. Он предоставляет поиск и пагинацию глобальных
аккаунтов, количество membership, безопасные provider badges, изменение
статусов, JSON-экспорт и анонимизирующее удаление. Чтения и разрушительные
операции записываются в структурированный аудит. Для изменений требуется
ограниченный machine-readable reason code; свободный текст с PII отклоняется и
не попадает в логи.

| Метод | Назначение |
|-------|------------|
| `ListCustomerAccounts` | Поиск и фильтрация глобальных аккаунтов |
| `GetCustomerAccount` | Детали аккаунта, число membership и безопасные provider metadata |
| `ListAccountStorefrontMemberships` | Membership аккаунта с фильтром по витрине и статусу |
| `SetCustomerAccountStatus` | Блокировка или активация глобального аккаунта |
| `SetStorefrontMembershipStatus` | Активация, блокировка или отзыв membership |
| `ExportCustomerAccount` | Безопасный JSON-экспорт |
| `DeleteCustomerAccount` | Анонимизация аккаунта и связанных данных |

Ответы не содержат provider authority, provider subject, provider user ID,
токены или другие необработанные идентификаторы провайдера.

## Privacy-safe метрики аккаунта клиента

Meter `DKH.CustomerService.CustomerAccount` экспортирует counters и latency
histograms для identity link/rejection/unlink/recovery и membership
first/returning/revoked touch. Labels ограничены определённым сервером
`storefront_id`, закрытым `outcome` и bounded `provider_kind` (`telegram`,
`email`, `wechat`, `google`, `apple` или `other`).

Email, телефон, имя, адрес, Keycloak/provider subject, customer/account/user id,
токены, секреты и произвольные ошибки никогда не становятся metric labels.
Неизвестный raw provider сворачивается в `other`; `CustomerAccountMetricsTests`
проверяет allowlist и отсутствие raw provider values в measurements. Это
агрегированные series без opaque attempt id, поэтому для них применяется общая
platform metrics retention policy.

*Последнее обновление: апрель 2026*

## Текущая связь активного профиля (v2)

Новый `CurrentProfileBindingService.GetMyActiveProfileBinding` возвращает настоящий профиль и витрину проверенного персонального пользователя. [Границы полномочий и актуальности](current-active-profile-binding.md). Контракт1.12 пока не опубликован.
