from sqlalchemy import create_engine, Column, String, Boolean, Integer, Text
from sqlalchemy.orm import declarative_base, sessionmaker

from .config import get_settings

settings = get_settings()

connect_args = {}
if settings.database_url.startswith("sqlite"):
    connect_args = {"check_same_thread": False}

engine = create_engine(settings.database_url, connect_args=connect_args)
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)
Base = declarative_base()


class PolicyProfile(Base):
    __tablename__ = "policy_profiles"

    id = Column(String, primary_key=True, index=True)
    tenant_id = Column(String, nullable=False, index=True)
    application_id = Column(String, nullable=False, index=True)
    name = Column(String, nullable=False)
    scope = Column(String, nullable=False, default="Application")
    domain = Column(String, nullable=True)
    is_active = Column(Boolean, nullable=False, default=True)
    version = Column(Integer, nullable=False, default=1)
    policy_json = Column(Text, nullable=True)
    effective_from = Column(String, nullable=False)
    created_at = Column(String, nullable=False)
    description = Column(String, nullable=True)


class AuditEvent(Base):
    __tablename__ = "audit_events"

    id = Column(String, primary_key=True, index=True)
    tenant_id = Column(String, nullable=False, index=True)
    application_id = Column(String, nullable=False, index=True)
    correlation_id = Column(String, nullable=True)
    description = Column(Text, nullable=True)
    risk_level = Column(String, nullable=True)
    decision = Column(String, nullable=True)
    created_at = Column(String, nullable=False)


def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
