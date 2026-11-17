namespace TextileConnect.Tests;

/// <summary>
/// The money and order rules again, on a real MySQL database built from the migrations (foreign keys, unique indexes, collation, row locks, datetime precision).
/// Each test runs an existing in-memory test unchanged, only the store is different. They are skipped unless TEXTILECONNECT_TEST_MYSQL is set (docs/TESTING.md).
/// </summary>
public sealed class MySqlSuiteTests
{
    // The whole sign-up to payout journey, over HTTP, with data created only through the API
    [MySqlFact] public Task Full_journey() => MySqlStore.RunAsync(() => new FullJourneyHttpTests().FullJourneyAsync());
    [MySqlFact] public Task Seller_does_not_accept_in_time_order_is_cancelled_and_refunded() => MySqlStore.RunAsync(() => new JourneyVariantsHttpTests().SellerDoesNotAcceptInTime_OrderIsCancelledAndRefundedAsync());
    [MySqlFact] public Task Retailer_reports_a_problem_payout_stays_on_hold() => MySqlStore.RunAsync(() => new JourneyVariantsHttpTests().RetailerReportsAProblem_PayoutStaysOnHoldAsync());

    // Checkout split
    [MySqlFact] public Task Checkout_splits_by_seller_and_computes_every_amount_on_the_server() => MySqlStore.RunAsync(() => new CheckoutHttpTests().Checkout_splits_by_seller_and_computes_every_amount_on_the_serverAsync());
    [MySqlFact] public Task Checkout_is_idempotent_for_the_same_key() => MySqlStore.RunAsync(() => new CheckoutHttpTests().Checkout_is_idempotent_for_the_same_key_and_rejects_key_reuseAsync());

    // Payment confirmation and webhook idempotency
    [MySqlFact] public Task Payment_start_is_idempotent_and_completion_marks_the_order_paid_once() => MySqlStore.RunAsync(() => new PaymentHttpTests().Payment_start_is_idempotent_and_completion_marks_the_order_paid_onceAsync());
    [MySqlFact] public Task Webhook_verifies_signature_amount_and_is_idempotent() => MySqlStore.RunAsync(() => new PaymentHttpTests().Webhook_verifies_signature_amount_and_is_idempotentAsync());
    [MySqlFact] public Task Replaying_an_event_id_has_no_second_effect() => MySqlStore.RunAsync(() => new WebhookEventHttpTests().Replaying_an_event_id_returns_200_and_has_no_second_effectAsync());

    // Refund on rejection and expiry, late payment
    [MySqlFact] public Task Rejecting_releases_stock_and_refunds_in_full() => MySqlStore.RunAsync(() => new SellerOrderHttpTests().Rejecting_releases_stock_refunds_in_full_and_needs_a_reasonAsync());
    [MySqlFact] public Task Unaccepted_orders_expire_cancel_and_refund() => MySqlStore.RunAsync(() => new SellerOrderHttpTests().Unaccepted_orders_expire_cancel_and_refund_with_a_fake_clockAsync());
    [MySqlFact] public Task Payment_that_arrives_after_cancellation_is_refunded() => MySqlStore.RunAsync(() => new SellerOrderHttpTests().Payment_that_arrives_after_cancellation_is_refunded_not_keptAsync());

    // Settlement hold and release, claim hold
    [MySqlFact] public Task Payout_is_held_then_scheduled_and_released_after_seven_days() => MySqlStore.RunAsync(() => new PayoutHttpTests().Payout_is_held_then_scheduled_on_dispatch_and_released_after_seven_daysAsync());
    [MySqlFact] public Task Confirmed_delivery_releases_the_payout() => MySqlStore.RunAsync(() => new PayoutHttpTests().Confirmed_delivery_releases_the_payout_at_the_next_runAsync());
    [MySqlFact] public Task Open_claim_keeps_the_payout_on_hold() => MySqlStore.RunAsync(() => new PayoutHttpTests().Open_claim_keeps_the_payout_on_hold_until_resolvedAsync());
    [MySqlFact] public Task Claim_decision_releases_the_payout_minus_the_refund() => MySqlStore.RunAsync(() => new ReturnClaimHttpTests().Open_claim_holds_the_payout_and_the_decision_releases_it_minus_the_refundAsync());

    // Filter counts and the stored popularity counter
    [MySqlFact] public Task Without_filters_every_facet_counts_the_whole_catalogue() => MySqlStore.RunAsync(() => new FilterFacetsHttpTests().Without_filters_every_facet_counts_the_whole_catalogueAsync());
    [MySqlFact] public Task A_filter_narrows_the_other_facets_but_not_its_own() => MySqlStore.RunAsync(() => new FilterFacetsHttpTests().A_filter_narrows_the_other_facets_but_not_its_ownAsync());
    [MySqlFact] public Task Delivery_adds_the_pieces_to_the_units_sold_counter() => MySqlStore.RunAsync(() => new UnitsSoldCounterTests().Delivery_adds_the_pieces_once_and_the_search_shows_themAsync());

    // Stock under concurrency
    [MySqlFact] public Task Two_checkouts_for_the_last_stock_only_one_wins() => MySqlStore.RunAsync(() => new ConcurrencyHttpTests().Two_checkouts_for_the_last_stock_only_one_winsAsync());
}
