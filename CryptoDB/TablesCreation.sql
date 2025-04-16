CREATE TABLE asset (
    id BIGINT PRIMARY KEY IDENTITY,
    symbol VARCHAR(255),
    precision INT,
    deposit_status VARCHAR(255),
    withdrawal_status VARCHAR(255),
    base_withdrawal_fee DECIMAL(18,8),
    min_withdrawal_amount DECIMAL(18,8)
);

CREATE TABLE crypto_index (
    id BIGINT PRIMARY KEY IDENTITY,
    symbol VARCHAR(255),
    constituent_exchanges varchar(200),
    underlying_asset_id BIGINT,
    quoting_asset_id BIGINT,
    tick_size DECIMAL(18,8),
    index_type VARCHAR(255),
    FOREIGN KEY (underlying_asset_id) REFERENCES asset(id),
    FOREIGN KEY (quoting_asset_id) REFERENCES asset(id)
);

CREATE TABLE product (
    id BIGINT PRIMARY KEY IDENTITY,
    symbol VARCHAR(255),
    description TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ,
    settlement_time TIMESTAMP,
    notional_type VARCHAR(255),
    impact_size INT,
    initial_margin DECIMAL(18,8),
    maintenance_margin DECIMAL(18,8),
    contract_value DECIMAL(18,8),
    contract_unit_currency VARCHAR(255),
    tick_size DECIMAL(18,8),
    product_specs Varchar(100),
    state VARCHAR(255),
    trading_status VARCHAR(255),
    max_leverage_notional DECIMAL(18,8),
    default_leverage DECIMAL(18,8),
    initial_margin_scaling_factor DECIMAL(18,8),
    maintenance_margin_scaling_factor DECIMAL(18,8),
    taker_commission_rate DECIMAL(18,8),
    maker_commission_rate DECIMAL(18,8),
    liquidation_penalty_factor DECIMAL(18,8),
    contract_type VARCHAR(255),
    position_size_limit INT,
    basis_factor_max_limit DECIMAL(18,8),
    is_quanto bit,
    funding_method VARCHAR(255),
    annualized_funding DECIMAL(18,8),
    price_band DECIMAL(18,8),
    underlying_asset_id BIGINT,
    quoting_asset_id BIGINT,
    settling_asset_id BIGINT,
	spot_index BIGINT,
    FOREIGN KEY (spot_index) REFERENCES crypto_index(id),
    FOREIGN KEY (underlying_asset_id) REFERENCES asset(id),
    FOREIGN KEY (quoting_asset_id) REFERENCES asset(id),
    FOREIGN KEY (settling_asset_id) REFERENCES asset(id)
);

CREATE TABLE orders (
    id BIGINT PRIMARY KEY IDENTITY,
    user_id BIGINT,
    size INT,
    unfilled_size INT,
    side VARCHAR(255),
    order_type VARCHAR(255),
    limit_price DOUBLE(18,8),
    stop_order_type VARCHAR(255),
    stop_price DECIMAL(18,8),
    paid_commission DECIMAL(18,8),
    commission DECIMAL(18,8),
    reduce_only BIT,
    client_order_id VARCHAR(255),
    state VARCHAR(255),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    product_id BIGINT,
    product_symbol VARCHAR(255),
    FOREIGN KEY (product_id) REFERENCES product(id)
);

CREATE TABLE position (
    user_id BIGINT,
    size INT,
    entry_price DECIMAL(18,8),
    margin DECIMAL(18,8),
    liquidation_price DECIMAL(18,8),
    bankruptcy_price DECIMAL(18,8),
    adl_level INT,
    product_id BIGINT,
    product_symbol VARCHAR(255),
    commission DECIMAL(18,8),
    realized_pnl DECIMAL(18,8),
    realized_funding DECIMAL(18,8),
    FOREIGN KEY (product_id) REFERENCES product(id)
);

