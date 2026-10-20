# Development

## Clean up filemodes

```bash
find . -type f -not -path '*/.git' -exec chmod 644 {} +
find . -type d -not -path '*/.git*' -exec chmod 755 {} +
```
