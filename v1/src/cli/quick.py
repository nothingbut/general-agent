"""Quick query mode implementation."""
import uuid
import logging
from pathlib import Path
from typing import Optional
from datetime import datetime

from .core_init import initialize_database, initialize_executor
from ..storage.models import Session

logger = logging.getLogger(__name__)

# Default database path
DB_PATH = Path("data/general_agent.db")


async def _create_temporary_session(db, query: str) -> str:
    """
    Create a temporary session and return its ID.

    Args:
        db: Database instance
        query: User query string (used for session title)

    Returns:
        Session ID of the created temporary session
    """
    temp_session_id = f"cli-{uuid.uuid4()}"
    now = datetime.now()
    temp_session = Session(
        id=temp_session_id,
        title=query[:50],
        created_at=now,
        updated_at=now,
        metadata={"type": "quick_query", "temporary": True}
    )
    await db.create_session(temp_session)
    logger.debug(f"Created temporary session: {temp_session_id}")
    return temp_session_id


async def _ensure_session_exists(db, session_id: str) -> None:
    """
    Ensure session exists, create if not.

    Args:
        db: Database instance
        session_id: Session ID to check/create
    """
    existing_session = await db.get_session(session_id)
    if existing_session is None:
        now = datetime.now()
        new_session = Session(
            id=session_id,
            title="Quick Query Session",
            created_at=now,
            updated_at=now,
            metadata={"type": "quick_query"}
        )
        await db.create_session(new_session)
        logger.debug(f"Created new session: {session_id}")


async def run_quick_query(
    query: str,
    session_id: Optional[str] = None,
    verbose: bool = False
) -> str:
    """
    Execute a quick query and return the response.

    Args:
        query: User query string
        session_id: Optional session ID. If not provided, creates a temporary session
        verbose: Enable verbose logging

    Returns:
        Agent response as a string

    Raises:
        None - All errors are caught and returned as user-friendly error messages
    """
    # Validate input
    if not query or not query.strip():
        return "Error: Query cannot be empty"

    db = None

    try:
        # Initialize database
        db = await initialize_database(DB_PATH)

        # Initialize executor
        executor = await initialize_executor(db, verbose=verbose)

        # Create or use session
        if session_id is None:
            session_id = await _create_temporary_session(db, query)
        else:
            await _ensure_session_exists(db, session_id)

        # Execute query
        result = await executor.execute(user_input=query, session_id=session_id)

        # Extract response
        response = result.get("response", "")

        if not result.get("success", True):
            error = result.get("error", "Unknown error")
            logger.warning(f"Query execution failed: {error}")

        return response

    except ConnectionError as e:
        error_msg = f"Connection error: Unable to connect to the service. Please ensure Ollama is running. Details: {str(e)}"
        logger.error(error_msg)
        return error_msg

    except TimeoutError as e:
        error_msg = f"Timeout error: The request took too long to complete. The model may be slow or overloaded. Details: {str(e)}"
        logger.error(error_msg)
        return error_msg

    except ValueError as e:
        error_msg = f"Input error: {str(e)}"
        logger.error(error_msg)
        return error_msg

    except Exception as e:
        error_msg = f"Unexpected error: {str(e)}"
        logger.exception("Error during quick query execution")
        return error_msg

    finally:
        # Always clean up database connection
        if db is not None:
            try:
                await db.close()
                if verbose:
                    logger.debug("Database connection closed")
            except Exception as e:
                logger.warning(f"Failed to close database connection: {e}")
