FROM python:3.12-slim
WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app/ app/
COPY policies/ policies/
COPY evaluations/ evaluations/
COPY src/Guardrail.API/wwwroot/ wwwroot/

ENV HF_TOKEN=""
ENV POLICY_SEED_PATH="/app/policies/samples"
ENV EVALUATION_SEED_PATH="/app/evaluations/datasets"

EXPOSE 7860

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "7860"]
