FROM python:3.12-slim
WORKDIR /app
COPY remapped_catalog/requirements.txt /app/remapped_catalog/requirements.txt
RUN pip install --no-cache-dir -r /app/remapped_catalog/requirements.txt
COPY remapped_catalog/ /app/remapped_catalog/
COPY website/ /app/website/
ENV PYTHONUNBUFFERED=1 PORT=80
EXPOSE 80
CMD ["python", "-u", "/app/remapped_catalog/server.py"]
