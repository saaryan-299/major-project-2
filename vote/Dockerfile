FROM python:3.9-slim

RUN apt-get update && apt-get install -y --no-install-recommends curl
RUN rm -rf /var/lib/apt/lists/*
RUN pip install Flask Redis gunicorn

WORKDIR /app
COPY . .

EXPOSE 80
CMD ["gunicorn", "app:app", "-b", "0.0.0.0:80", "--log-file", "-", "--access-logfile", "-", "--workers", "4", "--keep-alive", "0"]
